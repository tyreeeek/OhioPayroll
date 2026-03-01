using System;
using System.Globalization;

namespace OhioPayroll.App.Services;

public static class TaxCalendarHelper
{
    public static (DateTime start, DateTime end) GetQuarterDates(int year, int quarter) => quarter switch
    {
        1 => (new DateTime(year, 1, 1), new DateTime(year, 3, 31)),
        2 => (new DateTime(year, 4, 1), new DateTime(year, 6, 30)),
        3 => (new DateTime(year, 7, 1), new DateTime(year, 9, 30)),
        4 => (new DateTime(year, 10, 1), new DateTime(year, 12, 31)),
        _ => throw new ArgumentOutOfRangeException(nameof(quarter))
    };

    public static string GetForm941DueDate(int year, int quarter) =>
        FormatDueDate(GetAdjustedForm941DueDate(year, quarter));

    public static DateTime GetForm941DueDateValue(int year, int quarter) =>
        GetAdjustedForm941DueDate(year, quarter);

    private static DateTime GetAdjustedForm941DueDate(int year, int quarter) => quarter switch
    {
        1 => AdjustToBusinessDay(new DateTime(year, 4, 30)),
        2 => AdjustToBusinessDay(new DateTime(year, 7, 31)),
        3 => AdjustToBusinessDay(new DateTime(year, 10, 31)),
        4 => AdjustToBusinessDay(new DateTime(year + 1, 1, 31)),
        _ => throw new ArgumentOutOfRangeException(nameof(quarter))
    };

    /// <summary>
    /// Advances the date to the next business day if it falls on a weekend or U.S. federal holiday.
    /// </summary>
    private static DateTime AdjustToBusinessDay(DateTime date)
    {
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || IsFederalHoliday(date))
            date = date.AddDays(1);
        return date;
    }

    /// <summary>
    /// Checks whether the given date is an observed U.S. federal holiday.
    /// Covers the 11 federal holidays recognized by the OPM.
    /// </summary>
    private static bool IsFederalHoliday(DateTime date)
    {
        int year = date.Year;
        int month = date.Month;
        int day = date.Day;

        // New Year's Day — January 1 (observed)
        if (month == 1 && day == 1) return true;
        if (month == 12 && day == 31 && date.DayOfWeek == DayOfWeek.Friday) return true; // observed Fri
        if (month == 1 && day == 2 && date.DayOfWeek == DayOfWeek.Monday) return true;   // observed Mon

        // Martin Luther King Jr. Day — 3rd Monday in January
        if (month == 1 && date.DayOfWeek == DayOfWeek.Monday && day >= 15 && day <= 21) return true;

        // Presidents' Day — 3rd Monday in February
        if (month == 2 && date.DayOfWeek == DayOfWeek.Monday && day >= 15 && day <= 21) return true;

        // Memorial Day — Last Monday in May
        if (month == 5 && date.DayOfWeek == DayOfWeek.Monday && day >= 25) return true;

        // Juneteenth — June 19 (observed)
        if (IsObservedFixedHoliday(date, month: 6, fixedDay: 19)) return true;

        // Independence Day — July 4 (observed)
        if (IsObservedFixedHoliday(date, month: 7, fixedDay: 4)) return true;

        // Labor Day — 1st Monday in September
        if (month == 9 && date.DayOfWeek == DayOfWeek.Monday && day <= 7) return true;

        // Columbus Day — 2nd Monday in October
        if (month == 10 && date.DayOfWeek == DayOfWeek.Monday && day >= 8 && day <= 14) return true;

        // Veterans Day — November 11 (observed)
        if (IsObservedFixedHoliday(date, month: 11, fixedDay: 11)) return true;

        // Thanksgiving Day — 4th Thursday in November
        if (month == 11 && date.DayOfWeek == DayOfWeek.Thursday && day >= 22 && day <= 28) return true;

        // Christmas Day — December 25 (observed)
        if (IsObservedFixedHoliday(date, month: 12, fixedDay: 25)) return true;

        return false;
    }

    /// <summary>
    /// Returns true if the date is the observed date for a fixed-date holiday.
    /// If the holiday falls on Saturday, it's observed on Friday; if Sunday, on Monday.
    /// </summary>
    private static bool IsObservedFixedHoliday(DateTime date, int month, int fixedDay)
    {
        if (date.Month != month) return false;

        var holiday = new DateTime(date.Year, month, fixedDay);
        var observed = holiday.DayOfWeek switch
        {
            DayOfWeek.Saturday => holiday.AddDays(-1),
            DayOfWeek.Sunday => holiday.AddDays(1),
            _ => holiday
        };
        return date.Date == observed.Date;
    }

    private static string FormatDueDate(DateTime date) =>
        date.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);
}
