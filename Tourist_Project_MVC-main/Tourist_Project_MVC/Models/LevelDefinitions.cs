namespace Tourist_Project_MVC.Models
{
    public static class LevelDefinitions
    {
        public static readonly (int Level, string Name, string Icon, int MinXP)[] Levels = new[]
        {
            (1, "Novice Explorer",   "🧭",   0),
            (2, "Desert Wanderer",   "🏜️", 100),
            (3, "Temple Seeker",     "🏛️", 300),
            (4, "Nile Voyager",      "⛵",  600),
            (5, "Pharaoh's Agent",   "👁️", 1000),
            (6, "Royal Guard",       "🛡️", 1500),
            (7, "Egypt Legend",      "👑", 2500),
        };

        public static (int Level, string Name, string Icon) GetLevel(int xp)
        {
            for (var i = Levels.Length - 1; i >= 0; i--)
            {
                if (xp >= Levels[i].MinXP)
                    return (Levels[i].Level, Levels[i].Name, Levels[i].Icon);
            }
            return (Levels[0].Level, Levels[0].Name, Levels[0].Icon);
        }

        public static (int Level, string Name, string Icon) GetLevelByNumber(int levelNumber)
        {
            foreach (var l in Levels)
            {
                if (l.Level == levelNumber)
                    return (l.Level, l.Name, l.Icon);
            }
            return (Levels[0].Level, Levels[0].Name, Levels[0].Icon);
        }

        public static int GetNextLevelXP(int currentXP)
        {
            foreach (var l in Levels)
            {
                if (l.MinXP > currentXP)
                    return l.MinXP;
            }
            return currentXP;
        }
    }
}