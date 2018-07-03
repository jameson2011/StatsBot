namespace StatsBot

type SystemStats =
    {
        npcKills: int;
        podKills: int;
        shipKills: int;
        jumps: int;
        systemId: int;
        name: string;
        level: string;
        regionName: string;
        isIncursion: bool;
        isFw: bool;
    }

type SystemJumps =
    {
        systemId: int;
        jumps: int;
    }


