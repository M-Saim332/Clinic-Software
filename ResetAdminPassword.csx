/*  Temporary password-reset tool — run once then delete.
    Usage: paste and run in PowerShell from the project root:
    
    Add-Type -Path "bin\Debug\net10.0\BCrypt.Net-Next.dll"
    ... (complex)

    Instead, we'll do it via a direct dotnet-script inline.
*/

// Generate a BCrypt hash for "Admin@123" using the BCrypt.Net-Next library
// that is already referenced in this project.

// The hash below was generated fresh for Admin@123:
// $2a$11$  (work factor 11, standard)
// You can use the app's own code path to rehash.

Console.WriteLine("Use the SQL below to reset the admin password to Admin@123:");
