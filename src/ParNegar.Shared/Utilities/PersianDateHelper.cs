using System.Globalization;

namespace ParNegar.Shared.Utilities;

/// <summary>
/// Helper class for Persian date conversions
/// کلاس کمکی برای تبدیل تاریخ شمسی
/// </summary>
public static class PersianDateHelper
{
    private static readonly PersianCalendar _persianCalendar = new PersianCalendar();

    /// <summary>
    /// Convert Gregorian date to Persian date string (yyyy/MM/dd format)
    /// </summary>
    public static string ConvertToPersianDate(DateTime gregorianDate)
    {
        try
        {
            int year = _persianCalendar.GetYear(gregorianDate);
            int month = _persianCalendar.GetMonth(gregorianDate);
            int day = _persianCalendar.GetDayOfMonth(gregorianDate);

            return $"{year:0000}/{month:00}/{day:00}";
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error converting date {gregorianDate:yyyy-MM-dd} to Persian date: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Convert Gregorian date to Persian date string - handles nullable dates
    /// </summary>
    public static string? ConvertToPersianDate(DateTime? gregorianDate)
    {
        if (gregorianDate == null)
            return null;

        return ConvertToPersianDate(gregorianDate.Value);
    }

    /// <summary>
    /// Safely convert Gregorian date to Persian date string - returns null on error
    /// </summary>
    public static string? ConvertToPersianDateSafe(DateTime? gregorianDate)
    {
        if (gregorianDate == null)
            return null;

        try
        {
            return ConvertToPersianDate(gregorianDate.Value);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Convert DateTimeOffset to Persian date string (extracts DateTime first)
    /// </summary>
    public static string? ConvertToPersianDateSafe(DateTimeOffset? gregorianDate)
    {
        if (gregorianDate == null)
            return null;

        try
        {
            return ConvertToPersianDate(gregorianDate.Value.DateTime);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Convert Persian date string to Gregorian date
    /// </summary>
    public static DateTime ConvertToGregorianDate(string persianDateString)
    {
        if (string.IsNullOrWhiteSpace(persianDateString))
            throw new ArgumentException("Persian date string cannot be null or empty", nameof(persianDateString));

        try
        {
            string[] parts = persianDateString.Split('/');
            if (parts.Length != 3)
                throw new ArgumentException("Persian date must be in yyyy/MM/dd format", nameof(persianDateString));

            int year = int.Parse(parts[0]);
            int month = int.Parse(parts[1]);
            int day = int.Parse(parts[2]);

            return _persianCalendar.ToDateTime(year, month, day, 0, 0, 0, 0);
        }
        catch (Exception ex) when (!(ex is ArgumentException))
        {
            throw new ArgumentException($"Invalid Persian date format: {persianDateString}. Expected format: yyyy/MM/dd", nameof(persianDateString), ex);
        }
    }

    /// <summary>
    /// Get current Persian date string
    /// </summary>
    public static string GetCurrentPersianDate()
    {
        return ConvertToPersianDate(DateTime.Now);
    }

    /// <summary>
    /// Validate Persian date string format
    /// </summary>
    public static bool IsValidPersianDateFormat(string? persianDateString)
    {
        if (string.IsNullOrWhiteSpace(persianDateString))
            return false;

        try
        {
            ConvertToGregorianDate(persianDateString);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
