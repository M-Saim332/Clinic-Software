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

    // Digits, spaces, +, -, (, ) only. Minimum 6 digits.
    public static bool IsValidPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return true; // Optional fields return true if empty. For required, check empty before calling.
        var digitCount = 0;
        foreach (var c in phone)
        {
            if (char.IsDigit(c)) digitCount++;
            else if (c != ' ' && c != '+' && c != '-' && c != '(' && c != ')') return false;
        }
        return digitCount >= 6;
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

    // Typical CNIC pattern e.g., 12345-1234567-1
    public static bool IsValidCNIC(string? cnic)
    {
        if (string.IsNullOrWhiteSpace(cnic)) return true;
        // Strip non-digits to check length
        var digits = Regex.Replace(cnic, @"\D", "");
        return digits.Length >= 13;
    }
}
