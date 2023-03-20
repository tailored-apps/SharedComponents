using System;
using Xunit;

namespace TailoredApps.Shared.DateTime.Tests
{
    public class DateTimeProviderTests
    {
        private static IDateTimeProvider provider = new DateTimeProvider();

        [Fact]
        public void As_Library_User_When_Get_Now_I_Get_Mine_System_Date_In_Current_Timezone_And_Local_Kind()
        {
            //arrange
            var dateTimeBeforeTest = System.DateTime.Now;
            var dateFromProvider = provider.Now;
            var dateTimeAfterTest = System.DateTime.Now;

            //verify

            Assert.InRange(dateFromProvider, dateTimeBeforeTest, dateTimeAfterTest);
            Assert.Equal(DateTimeKind.Local, dateFromProvider.Kind);
        }

        [Fact]
        public void As_Library_User_When_Get_Today_I_Get_Mine_System_Date_In_Current_Timezone_And_Local_Kind()
        {
            //arrange
            var dateBeforeTest = System.DateTime.Today;
            var dateFromProvider = provider.Today;
            var dateAfterTest = System.DateTime.Today;

            //verify

            Assert.InRange(dateFromProvider, dateBeforeTest, dateAfterTest);
            Assert.Equal(dateBeforeTest, dateFromProvider);
            Assert.Equal(dateAfterTest, dateFromProvider);
            Assert.Equal(0, dateFromProvider.Hour);
            Assert.Equal(0, dateFromProvider.Minute);
            Assert.Equal(0, dateFromProvider.Second);
            Assert.Equal(0, dateFromProvider.Millisecond);
            Assert.Equal(DateTimeKind.Local, dateFromProvider.Kind);
        }

        [Fact]
        public void As_Library_User_When_Get_UtcNow_I_Get_Date_In_Utc_Timezone_And_Utc_Kind()
        {
            //arrange
            var dateTimeBeforeTest = System.DateTime.UtcNow;
            var dateFromProvider = provider.UtcNow;
            var dateTimeAfterTest = System.DateTime.UtcNow;

            //verify

            Assert.InRange(dateFromProvider, dateTimeBeforeTest, dateTimeAfterTest);
            Assert.Equal(DateTimeKind.Utc, dateFromProvider.Kind);
        }

        [Fact]
        public void As_Library_User_When_Get_UtcToday_I_Get_Date_In_Utc_Timezone_And_Utc_Kind()
        {
            //arrange
            var dateBeforeTest = System.DateTime.UtcNow.Date;
            var dateFromProvider = provider.UtcToday;
            var dateAfterTest = System.DateTime.UtcNow.Date;

            //verify

            Assert.InRange(dateFromProvider, dateBeforeTest, dateAfterTest);
            Assert.Equal(dateBeforeTest, dateFromProvider);
            Assert.Equal(dateAfterTest, dateFromProvider);
            Assert.Equal(0, dateFromProvider.Hour);
            Assert.Equal(0, dateFromProvider.Minute);
            Assert.Equal(0, dateFromProvider.Second);
            Assert.Equal(0, dateFromProvider.Millisecond);
            Assert.Equal(DateTimeKind.Utc, dateFromProvider.Kind);
        }

        [Fact]
        public void As_Library_User_When_Get_TimeOfDay_I_Get_TimeSpan_In_Current_Timezone()
        {
            //arrange
            var dateTimeBeforeTest = System.DateTime.Now.TimeOfDay;
            var dateFromProvider = provider.TimeOfDay;
            var dateTimeAfterTest = System.DateTime.Now.TimeOfDay;

            //verify

            Assert.InRange(dateFromProvider, dateTimeBeforeTest, dateTimeAfterTest);
        }

        [Fact]
        public void As_Library_User_When_Get_UtcTimeOfDay_I_Get_TimeSpan_In_Utc_Timezone()
        {
            //arrange
            var dateTimeBeforeTest = System.DateTime.Now.TimeOfDay;
            var dateFromProvider = provider.TimeOfDay;
            var dateTimeAfterTest = System.DateTime.Now.TimeOfDay;

            //verify

            Assert.InRange(dateFromProvider, dateTimeBeforeTest, dateTimeAfterTest);
        }
    }
}