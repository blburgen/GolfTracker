using SQLite;
using GolfTracker.Models;

namespace GolfTracker.Data;

public class GolfDatabase
{
    private SQLiteAsyncConnection? _database;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private async Task Init()
    {
        if (_database is not null)
            return;

        await _initLock.WaitAsync();
        try
        {
            if (_database is not null)
                return;

            var db = new SQLiteAsyncConnection(Constants.DatabasePath, Constants.Flags);
            await db.CreateTableAsync<Course>();
            await db.CreateTableAsync<Round>();
            await db.CreateTableAsync<HoleScore>();

            _database = db;
        }
        finally
        {
            _initLock.Release();
        }
    }

    // ==================== COURSE METHODS ====================

    public async Task<List<Course>> GetCoursesAsync()
    {
        await Init();
        return await _database!.Table<Course>().ToListAsync();
    }

    public async Task<List<Course>> GetCoursesByTypeAsync(string courseType)
    {
        await Init();
        return await _database!.Table<Course>()
            .Where(c => c.CourseType == courseType)
            .ToListAsync();
    }

    public async Task<Course?> GetCourseAsync(int id)
    {
        await Init();
        return await _database!.Table<Course>()
            .Where(c => c.ID == id)
            .FirstOrDefaultAsync();
    }

    public async Task<int> SaveCourseAsync(Course course)
    {
        await Init();
        if (course.ID != 0)
            return await _database!.UpdateAsync(course);
        else
            return await _database!.InsertAsync(course);
    }

    public async Task<int> DeleteCourseAsync(Course course)
    {
        await Init();
        return await _database!.DeleteAsync(course);
    }

    // ==================== ROUND METHODS ====================

    public async Task<List<Round>> GetRoundsAsync()
    {
        await Init();
        return await _database!.Table<Round>()
            .OrderByDescending(r => r.DatePlayed)
            .ToListAsync();
    }

    public async Task<List<Round>> GetRoundsForCourseAsync(string courseName)
    {
        await Init();
        return await _database!.Table<Round>()
            .Where(r => r.CourseName == courseName)
            .OrderByDescending(r => r.DatePlayed)
            .ToListAsync();
    }

    public async Task<Round?> GetRoundAsync(int id)
    {
        await Init();
        return await _database!.Table<Round>()
            .Where(r => r.ID == id)
            .FirstOrDefaultAsync();
    }

    public async Task<int> SaveRoundAsync(Round round)
    {
        await Init();
        round.NeedsSync = true;
        round.LastModifiedUtc = DateTime.UtcNow;
        return round.ID != 0 ? await _database!.UpdateAsync(round) : await _database!.InsertAsync(round);
    }

    public async Task<int> DeleteRoundAsync(Round round)
    {
        await Init();
        // Delete associated hole scores first so no orphaned records remain
        var holes = await GetHoleScoresForRoundAsync(round.ID);
        foreach (var hole in holes)
        {
            await _database!.DeleteAsync(hole);
        }

        return await _database!.DeleteAsync(round);
    }

    // ==================== HOLE SCORE METHODS ====================

    public async Task<List<HoleScore>> GetHoleScoresForRoundAsync(int roundId)
    {
        await Init();
        return await _database!.Table<HoleScore>()
            .Where(h => h.RoundID == roundId)
            .OrderBy(h => h.HoleNumber)
            .ToListAsync();
    }

    public async Task<int> SaveHoleScoreAsync(HoleScore holeScore)
    {
        await Init();
        if (holeScore.ID != 0)
            return await _database!.UpdateAsync(holeScore);
        else
            return await _database!.InsertAsync(holeScore);
    }

    public async Task SaveAllHoleScoresAsync(IEnumerable<HoleScore> holeScores)
    {
        await Init();
        foreach (var hole in holeScores)
        {
            if (hole.ID != 0)
                await _database!.UpdateAsync(hole);
            else
                await _database!.InsertAsync(hole);
        }
    }
    // 
    public async Task<List<Round>> GetUnsyncedRoundsAsync()
    {
        await Init();
        return await _database!.Table<Round>()
            .Where(r => r.NeedsSync)
            .ToListAsync();
    }

    public async Task MarkRoundSyncedAsync(Round round)
    {
        await Init();
        round.NeedsSync = false;
        await _database!.UpdateAsync(round);
    }
}
