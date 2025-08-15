using STGenerator;

Race race = Race.Race2;

///////////////////////////////
// LOADING ENTRIES & CONTACTS//
///////////////////////////////

string[] siTimingFile = race switch
{
    Race.Race1 => File.ReadAllLines(@"X:\StartTimeGenerator\sitiming_r1.csv"),
    Race.Race2 => File.ReadAllLines(@"X:\StartTimeGenerator\sitiming_r2.csv")
};

List<Entry> entries = [];

int currentGroupId = 0;
Dictionary<string, int> groupIdMap = [];

string[] headers = siTimingFile[0]
                    .Split(',')
                    .Select(Trim)
                    .ToArray();

foreach (string line in siTimingFile.Skip(1))
{
    string[] cells = line.Split(',');

    string runnerIdStr = Trim(cells[0]);
    int runnerId = int.Parse(runnerIdStr);

    string classId = Trim(cells[10]);
    int course = CourseMap(classId);

    string startBlock = Trim(cells[12]);

    string fName = Trim(cells[4]);
    string lName = Trim(cells[5]);

    string rankingKey = Trim(cells[18]);

    string groupIdStr = Trim(cells[19]);
    int groupId = -1;
    if (groupIdMap.TryGetValue(groupIdStr, out int existingGroupId))
    {
        groupId = existingGroupId;
    }
    else
    {
        groupId = currentGroupId++;
        groupIdMap[groupIdStr] = groupId;
    }

    entries.Add(
        new Entry(runnerId, groupId, fName, lName, course, rankingKey, startBlock, cells)
    );
}

////////////////////////////
// LOADING WORLD RANKINGS //
////////////////////////////

string[] mRankingFile = File.ReadAllLines(@"X:\StartTimeGenerator\mranking.csv");
string[] wRankingFile = File.ReadAllLines(@"X:\StartTimeGenerator\wranking.csv");

bool useAverage = false;

Dictionary<string, int> mensRanking = [];
Dictionary<string, int> womensRanking = [];

foreach (string line in mRankingFile.Skip(1))
{
    string[] cells = line.Split(';');

    string rankingKey = Trim(cells[0]);

    string valueStr = Trim(useAverage ? cells[7] : cells[5]);
    int value = int.Parse(valueStr);

    mensRanking.Add(rankingKey, value);
}

foreach (string line in wRankingFile.Skip(1))
{
    string[] cells = line.Split(';');

    string rankingKey = Trim(cells[0]);

    string valueStr = Trim(useAverage ? cells[7] : cells[5]);
    int value = int.Parse(valueStr);

    womensRanking.Add(rankingKey, value);
}

/////////////////////////////
// CALCULATING START TIMES //
/////////////////////////////

EventSpecification eventSpec = new();
eventSpec.startInterval = TimeSpan.FromMinutes(1);

DateTime firstStart = race == Race.Race1 ?
    new(2025, 08, 03, 10, 00, 00) :
    new(2025, 08, 03, 13, 00, 00);

eventSpec.startBlocks = new Dictionary<string, TimeRange>
{
    { "Early", new(firstStart, TimeSpan.FromMinutes(30)) },
    { "Middle", new(firstStart + TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30)) },
    { "Late", new(firstStart + TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(30)) }
};

TimeRange[] availableRanges = [ new(firstStart, TimeSpan.FromMinutes(90)) ];

CourseSpecification c1Spec = new();
c1Spec.availableRanges = availableRanges;
c1Spec.startInterval = TimeSpan.FromMinutes(1);
c1Spec.overrideStartBlock = true;
c1Spec.seeding = mensRanking;
c1Spec.groupingSize = 1;
c1Spec.orderedBestLast = true;

if (race == Race.Race2)
{
    c1Spec.groupingSize = 10;
}

CourseSpecification c2Spec = new();
c2Spec.availableRanges = availableRanges;
c2Spec.startInterval = TimeSpan.FromMinutes(1);
c2Spec.overrideStartBlock = true;
c2Spec.seeding = womensRanking;
c2Spec.groupingSize = 1;
c2Spec.orderedBestLast = true;

if (race == Race.Race2)
{
    c2Spec.groupingSize = 10;
}

CourseSpecification c3Spec = new();
c3Spec.availableRanges = availableRanges;
c3Spec.startInterval = TimeSpan.FromMinutes(1);

CourseSpecification c4Spec = new();
c4Spec.availableRanges = availableRanges;
c4Spec.startInterval = TimeSpan.FromMinutes(1);

CourseSpecification c5Spec = new();
c5Spec.availableRanges = availableRanges;
c5Spec.startInterval = TimeSpan.FromMinutes(1);

CourseSpecification c6Spec = new();
c6Spec.availableRanges = availableRanges;
c6Spec.startInterval = TimeSpan.FromMinutes(1);

CourseSpecification c7Spec = new();
c7Spec.availableRanges = availableRanges;
c7Spec.startInterval = TimeSpan.FromMinutes(1);

CourseSpecification c8Spec = new();
c8Spec.availableRanges = availableRanges;
c8Spec.startInterval = TimeSpan.FromMinutes(1);

eventSpec.courseSpecs = new Dictionary<int, CourseSpecification>
{
    { 1, c1Spec },
    { 2, c2Spec },
    { 3, c3Spec },
    { 4, c4Spec },
    { 5, c5Spec },
    { 6, c6Spec },
    { 7, c7Spec },
    { 8, c8Spec }
};

StartTimeGenerator generator = new(eventSpec, entries, 12345);

generator.Generate();

////////////////////////
// EXPORT START TIMES //
////////////////////////

List<string> exportLines = [string.Join(',', headers)];

foreach (var (time, entry) in generator.StartTimes)
{
    //11

    string[] data = entry.rawData;

    data[0] = "";
    data[18] = "";
    data[19] = ""; // Validating SiTiming Imports

    data[3] = $"{data[3]}&{entry.RankingKey}"; // Update Membership for BOF & IOF
    data[11] = time.ToString("HH:mm:ss"); // Update the start time in the raw data

    exportLines.Add(string.Join(',', data));
}

string rText = race switch
{
    Race.Race1 => "r1",
    Race.Race2 => "r2"
};

File.WriteAllLines($@"X:\StartTimeGenerator\starttimes_{rText}.csv", exportLines);
Console.WriteLine("COMPLETE");


int CourseMap(string classId)
{
    return classId switch
    {
        "Men Open" => 1,

        "Women Open" => 2,

        "Veteran Men 40+" => 3,

        "Supervet Men 55+" => 4,
        "Veteran Women 40+" => 4,

        "Ultravet Men 65+" => 5,
        "Supervet Women 55+" => 5,

        "Hypervet Men 75+" => 6,
        "Ultravet Women 65+" => 6,
        "Hypervet Women 75+" => 6,

        "Junior Men 16-" => 7,
        "Junior Women 16-" => 7,

        "Young Junior Men 12-" => 8,
        "Young Junior Women 12-" => 8,

        _ => throw new ArgumentException($"Unknown class ID: {classId}")
    };
}

string Trim(string s)
{
    return s.Trim().Replace("\"", "").Trim();
}

enum Race
{
    Race1, Race2
}