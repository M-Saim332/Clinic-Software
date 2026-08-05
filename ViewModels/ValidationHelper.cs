using System;
using System.Text.RegularExpressions;

namespace ClinicSystem.UI.Helpers;

public static class ValidationHelper
{
    // Allows letters, spaces, hyphens, apostrophes. Minimum 2 characters.
    public static bool IsValidName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length < 2) return false;
        // Must contain at least one letter, no purely numeric strings allowed.
        return Regex.IsMatch(name, @"[a-zA-Z]") && !Regex.IsMatch(name, @"^[\d\s]+$");
    }

    public static string FormatPhone(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var digits = Regex.Replace(input, @"\D", "");
        if (digits.Length > 11) digits = digits.Substring(0, 11);
        return digits;
    }

    // Exactly 11 numeric digits
    public static bool IsValidPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return true; // Optional fields return true if empty. For required, check empty before calling.
        return Regex.IsMatch(phone, @"^\d{11}$");
    }

    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return true;
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    public static string FormatCNIC(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var digits = Regex.Replace(input, @"\D", "");
        if (digits.Length > 13) digits = digits.Substring(0, 13);
        return digits;
    }

    public static bool ValidateCNIC(string? cnic, bool required)
    {
        if (string.IsNullOrWhiteSpace(cnic))
        {
            return !required;
        }
        return Regex.IsMatch(cnic, @"^\d{13}$");
    }
}
