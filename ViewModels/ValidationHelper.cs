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
        if (digits.Length <= 4) return digits;
        return $"{digits.Substring(0, 4)}-{digits.Substring(4)}";
    }

    // Digits, spaces, +, -, (, ) only. Minimum 6 digits.
    public static bool IsValidPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return true; // Optional fields return true if empty. For required, check empty before calling.
        return Regex.IsMatch(phone, @"^\d{4}-\d{7}$");
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
        if (digits.Length <= 5) return digits;
        if (digits.Length <= 12) return $"{digits.Substring(0, 5)}-{digits.Substring(5)}";
        return $"{digits.Substring(0, 5)}-{digits.Substring(5, 7)}-{digits.Substring(12)}";
    }

    // Typical CNIC pattern e.g., 12345-1234567-1
    public static bool IsValidCNIC(string? cnic)
    {
        if (string.IsNullOrWhiteSpace(cnic)) return true;
        return Regex.IsMatch(cnic, @"^\d{5}-\d{7}-\d{1}$");
    }
}
