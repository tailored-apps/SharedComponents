namespace TailoredApps.Shared.EntityFramework.Testing
{
    public class DbInMemoryCounters
    {
        public DbInMemoryCounters()
        {
            IntCounter = FirstIdInt;
            LongCounter = FirstIdLong;
        }

        public const int FirstIdInt = 1_000;
        public const long FirstIdLong = 1_000_000;

        public int IntCounter { get; set; }
        public long LongCounter { get; set; }

        public int IncrementInt()
        {
            return IntCounter += FirstIdInt;
        }

        public long IncrementLong()
        {
            return LongCounter += FirstIdLong;
        }
    }
}
