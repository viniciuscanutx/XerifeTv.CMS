namespace XerifeTv.CMS.Extensions;

public static class StringExtensions
{
    public static string CapitalizeFirstLetter(this string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        return char.ToUpper(value[0]) + value[1..];
    }
}