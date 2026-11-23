using SQLite;
using GolfTracker.Models;

namespace GolfTracker.Data;

// Manages local SQLite database access, table initialization, 
// and CRUD operations for courses, rounds, and hole scores.
public class GolfDatabase
{
    private SQLiteAsyncConnection? _database;

    // Ensures thread-safe, single initialization and closure of 
    // the SQLite connection.
    private readonly SemaphoreSlim _initLock = new(1, 1);

    // Expose the path so SyncService knows which file to upload/download
    public string DbPath => Constants.DatabasePath;

    // Initializes the SQLite connection and creates tables if they do 
    // not exist, using double-checked locking to prevent race conditions.
    public async Task Init()
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

    // Safely closes and disposes the active database connection (e.g., before 
    // overwriting the database file during a cloud sync).
    public async Task CloseConnectionAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            if (_database is not null)
            {
                await _database.CloseAsync();
                _database = null;
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    // ==================== COURSE METHODS ====================

    // Retrieves all saved golf courses.
    public async Task<List<Course>> GetCoursesAsync()
    {
        await Init();
        return await _database!.Table<Course>().ToListAsync();
    }

    // Retrieves all courses matching a specific type 
    // (e.g., 18-hole, 9-hole, par-3).
    public async Task<List<Course>> GetCoursesByTypeAsync(string courseType)
    {
        await Init();
        return await _database!.Table<Course>()
            .Where(c => c.CourseType == courseType)
            .ToListAsync();
    }

    // Retrieves a single course by its primary key ID, 
    // or null if not found.
    public async Task<Course?> GetCourseAsync(int id)
    {
        await Init();
        return await _database!.Table<Course>()
            .Where(c => c.ID == id)
            .FirstOrDefaultAsync();
    }

    // Inserts a new course or updates an 
    // existing one based on whether ID is non-zero.
    public async Task<int> SaveCourseAsync(Course course)
    {
        await Init();
        if (course.ID != 0)
            return await _database!.UpdateAsync(course);
        else
            return await _database!.InsertAsync(course);
    }

    // Deletes the specified course record from the database.
    public async Task<int> DeleteCourseAsync(Course course)
    {
        await Init();
        return await _database!.DeleteAsync(course);
    }

    // ==================== ROUND METHODS ====================

    // Retrieves all played rounds ordered from most recent to oldest.
    public async Task<List<Round>> GetRoundsAsync()
    {
        await Init();
        return await _database!.Table<Round>()
            .OrderByDescending(r => r.DatePlayed)
            .ToListAsync();
    }

    // Retrieves all played rounds for a specific course, 
    // ordered from most recent to oldest.
    public async Task<List<Round>> GetRoundsForCourseAsync(string courseName)
    {
        await Init();
        return await _database!.Table<Round>()
            .Where(r => r.CourseName == courseName)
            .OrderByDescending(r => r.DatePlayed)
            .ToListAsync();
    }

    // Retrieves a single round by its primary key ID, or null if not found.
    public async Task<Round?> GetRoundAsync(int id)
    {
        await Init();
        return await _database!.Table<Round>()
            .Where(r => r.ID == id)
            .FirstOrDefaultAsync();
    }

    // Saves a round and flags it as needing cloud synchronization.
    public async Task<int> SaveRoundAsync(Round round)
    {
        await Init();
        round.NeedsSync = true;
        round.LastModifiedUtc = DateTime.UtcNow;
        return round.ID != 0 ? await _database!.UpdateAsync(round) : await _database!.InsertAsync(round);
    }

    // Atomically saves a round and its associated hole scores in a single transaction, 
    // updating existing hole records to prevent duplicates.
    public async Task SaveRoundWithHoleScoresAsync(Round round, IEnumerable<HoleScore> holeScores)
    {
        await Init();
        round.NeedsSync = true;
        round.LastModifiedUtc = DateTime.UtcNow;
        var scores = holeScores.ToList();

        await _database!.RunInTransactionAsync(connection =>
        {
            if (round.ID != 0)
                connection.Update(round);
            else
                connection.Insert(round);

            // Fetch existing hole scores for this 
            // round to upsert rather than create duplicate entries
            var existingHoles = connection.Table<HoleScore>()
                .Where(h => h.RoundID == round.ID)
                .ToList();

            foreach (var holeScore in scores)
            {
                holeScore.RoundID = round.ID;
                var existingHole = existingHoles.FirstOrDefault(h => h.HoleNumber == holeScore.HoleNumber);
                if (existingHole is not null)
                {
                    holeScore.ID = existingHole.ID;
                    connection.Update(holeScore);
                }
                else
                {
                    connection.Insert(holeScore);
                }
            }
        });
    }

    // Deletes a round along with all of its associated hole score records.
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

    // Retrieves all hole scores for a given round, 
    // ordered sequentially by hole number.
    public async Task<List<HoleScore>> GetHoleScoresForRoundAsync(int roundId)
    {
        await Init();
        return await _database!.Table<HoleScore>()
            .Where(h => h.RoundID == roundId)
            .OrderBy(h => h.HoleNumber)
            .ToListAsync();
    }

    // Inserts a new hole score or updates an existing one.
    public async Task<int> SaveHoleScoreAsync(HoleScore holeScore)
    {
        await Init();
        if (holeScore.ID != 0)
            return await _database!.UpdateAsync(holeScore);
        else
            return await _database!.InsertAsync(holeScore);
    }

    // Iterates through a collection of hole scores and 
    // inserts or updates each record individually.
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

    // ==================== SYNC METHODS ====================

    // Retrieves all rounds that have local changes 
    // pending cloud synchronization.
    public async Task<List<Round>> GetUnsyncedRoundsAsync()
    {
        await Init();
        return await _database!.Table<Round>()
            .Where(r => r.NeedsSync)
            .ToListAsync();
    }

    // Clears the sync flag on a round after it has been 
    // successfully uploaded.
    public async Task MarkRoundSyncedAsync(Round round)
    {
        await Init();
        round.NeedsSync = false;
        await _database!.UpdateAsync(round);
    }
}
