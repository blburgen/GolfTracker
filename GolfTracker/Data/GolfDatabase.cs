using SQLite;
using GolfTracker.Models;

namespace GolfTracker.Data;

public class GolfDatabase
{
    private SQLiteAsyncConnection? _database;

    private async Task Init()
    {
        if (_database is not null)
            return;

        _database = new SQLiteAsyncConnection(Constants.DatabasePath, Constants.Flags);

        await _database.CreateTableAsync<Course>();
        await _database.CreateTableAsync<Round>();
        await _database.CreateTableAsync<HoleScore>();
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

    public async Task<List<Round>> GetRoundsForCourseAsync(int courseId)
    {
        await Init();
        return await _database!.Table<Round>()
            .Where(r => r.CourseID == courseId)
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
        if (round.ID != 0)
            return await _database!.UpdateAsync(round);
        else
            return await _database!.InsertAsync(round);
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
}
