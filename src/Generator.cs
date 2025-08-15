using System.Diagnostics.Contracts;

namespace STGenerator;

public struct EventSpecification
{
    public Dictionary<int, CourseSpecification> courseSpecs;
    public Dictionary<string, TimeRange> startBlocks;
    public TimeSpan startInterval;
}

public struct CourseSpecification
{
    public TimeRange[] availableRanges;
    public TimeSpan startInterval;

    public bool overrideStartBlock;
    public Dictionary<string, int> seeding;
    public int groupingSize;
    public bool orderedBestLast;
}

public struct Entry
{
    public int GroupId;
    public int RunnerId;

    public int Course;
    public string StartBlock;

    public string FirstName, LastName;

    public string RankingKey;

    public string[] rawData;

    public Entry(int runnerId, int groupId, string fName, string lName, int course, string rankingKey, string startBlock, string[] raw)
    {
        RunnerId = runnerId;
        GroupId = groupId;

        FirstName = fName;
        LastName = lName;

        Course = course;
        RankingKey = rankingKey;
        StartBlock = startBlock;

        rawData = raw;
    }
}

public record struct TimeRange(DateTime Start, DateTime End)
{
    public TimeRange(DateTime start, TimeSpan duration) : this(start, start + duration) { }
    public bool IsValid => Start < End;
    public static TimeRange Empty => new(DateTime.MinValue, DateTime.MinValue);
}


public class StartTimeGenerator
{
    Random rnd;
    EventSpecification eventSpec;

    int[] familyOrdering;
    List<Entry> entries;
    Dictionary<int, List<Entry>> entriesByFamily;

    List<(DateTime, Entry)> startTimes = [];

    public List<(DateTime, Entry)> StartTimes => startTimes;

    public StartTimeGenerator(EventSpecification specification, List<Entry> entries, int? seed)
    {
        rnd = seed.HasValue 
            ? new Random(seed.Value) : new Random();

        eventSpec = specification;

        this.entries = entries;
        entriesByFamily = entries
            .GroupBy(e => e.GroupId)
            .ToDictionary(g => g.Key, g => g.ToList());

        familyOrdering = entriesByFamily.Keys.OrderBy(k => k).ToArray();
        familyOrdering.Shuffle(rnd);
    }

    public void Generate()
    {
        foreach (int familyId in familyOrdering)
        {
            if (!entriesByFamily.TryGetValue(familyId, out List<Entry>? family))
                continue;

            var familyStartBlocks = family.GroupBy(e => e.StartBlock).Select(g => g.Key).ToArray() ;

            foreach (var startBlock in familyStartBlocks)
            {
                if (startBlock == "HELPER")
                    continue; // Skip helper entries

                DateTime baseTime = GenerateStartTime(eventSpec.startBlocks[startBlock]);

                foreach (Entry entry in family.Where(e => e.StartBlock == startBlock))
                {
                    var courseSpec = eventSpec.courseSpecs[entry.Course];

                    // Seeded course
                    if (courseSpec.overrideStartBlock)
                        continue;

                    DateTime currentStartTime = baseTime + TimeSpan.FromMinutes(rnd.Next(-5, 5));
                    int size = 1,  direction = 1;

                    int exitClause = 0;
                    bool valid = Validate(entry, currentStartTime);
                    while (!valid)
                    {
                        // +x
                        currentStartTime = baseTime + (eventSpec.startInterval * size * direction);
                        valid = Validate(entry, currentStartTime);

                        if (valid)
                            break;

                        direction *= -1;

                        // -x
                        currentStartTime = baseTime + (eventSpec.startInterval * size * direction);
                        valid = Validate(entry, currentStartTime);

                        size++;
                        direction *= -1;

                        exitClause++;
                        if (exitClause > 1000)
                        {
                            Console.WriteLine("OVER 1000 ATTEMPTS");
                            Console.ReadLine();
                        }
                    }

                    if (Trim(entry.rawData[2]) != "" && startTimes.Select(x => x.Item2.rawData[2]).Contains(entry.rawData[2]))
                        Console.WriteLine($"Duplicate Entry: {entry.FirstName} {entry.LastName}");
                    else
                        startTimes.Add((currentStartTime, entry));
                }
            }
        }

        var seededCourses = eventSpec.courseSpecs.Where(c => c.Value.overrideStartBlock);

        foreach (var (course, spec) in seededCourses)
        {

            List<Entry> courseEntries = entries.Where(e => e.Course == course && e.StartBlock != "HELPER").ToList();
            List<string> rankingKeys = courseEntries.Select(e => e.RankingKey).ToList();

            Dictionary<string, int> validRankings = spec.seeding.Where(s => rankingKeys.Contains(s.Key)).ToDictionary();

            // Select keys in order (high-low) based on value
            var orderedRankings = validRankings.OrderByDescending(kvp => kvp.Value)
                        .Select(kvp => kvp.Key)
                        .ToList();

            if (spec.groupingSize > 1)
            {
                var shuffledArray = orderedRankings.ToArray();
                shuffledArray.ShuffleEveryN(spec.groupingSize, rnd);

                orderedRankings = shuffledArray.ToList();
            }

            DateTime currTime = spec.availableRanges.Max(r => r.End);
            List<Entry> completedEntries = [];

            int currentIndex = 0;
            while (currentIndex < orderedRankings.Count)
            {
                Entry entry = courseEntries.FirstOrDefault(e => e.RankingKey == orderedRankings[currentIndex]);

                if (Validate(entry, currTime))
                {
                    startTimes.Add((currTime, entry));
                    completedEntries.Add(entry);
                    currentIndex++;
                }

                currTime -= spec.startInterval;
            }

            var missingEntries = courseEntries.Where(e => !completedEntries.Contains(e)).ToArray();
            missingEntries.Shuffle(rnd);

            Console.WriteLine($"Missing Entries: {missingEntries.Length}");

            foreach (Entry entry in missingEntries)
            {
                bool valid = false;
                while (!valid)
                {
                    valid = Validate(entry, currTime);

                    currTime -= spec.startInterval;
                }

                startTimes.Add((currTime + spec.startInterval, entry));
            }

        }

        Console.WriteLine("DONE");
    }

    DateTime GenerateStartTime(TimeRange startBlock)
    {
        if (!startBlock.IsValid)
            throw new ArgumentException("Invalid time range provided.", nameof(startBlock));

        // Dangerous
        int c = 0;
        while (true)
        {
            if (c++ > 1000)
                throw new InvalidOperationException("Failed to generate a valid start time after 1000 attempts.");

            TimeSpan range = startBlock.End - startBlock.Start;
            double indices = range / eventSpec.startInterval;
            
            int random = rnd.Next(0, (int)indices);

            DateTime startTime = startBlock.Start + (eventSpec.startInterval * random);

            // Should be unnecessary, but just in case (not sure how stuff works)
            if (startTime > startBlock.Start && startTime < startBlock.End)
                return startTime;
        }

    }

    bool Validate(Entry entry, DateTime startTime)
    {
        if (entry.FirstName == "Lucy" && entry.LastName == "Ward")
            Console.Write("");

        var courseSpec = eventSpec.courseSpecs[entry.Course];

        // Start time is within the available ranges for the course
        if (!courseSpec.availableRanges.Any(range => range.Start <= startTime && startTime <= range.End))
            return false;

        // Check conflicting start times (might be checked below as well)
        if (startTimes.Any(t => t.Item1 == startTime && t.Item2.Course == entry.Course))
            return false;

        // Check Adjacent Times for conflicts
        TimeSpan timeSpan = TimeSpan.Zero;
        while (timeSpan < courseSpec.startInterval)
        {
            if (startTimes.Any(t => t.Item1 == startTime + timeSpan && t.Item2.Course == entry.Course))
                return false;

            if (startTimes.Any(t => t.Item1 == startTime - timeSpan && t.Item2.Course == entry.Course))
                return false;

            timeSpan += eventSpec.startInterval;
        }

        return true;
    }


    string Trim(string s)
    {
        return s.Trim().Replace("\"", "").Trim();
    }
}