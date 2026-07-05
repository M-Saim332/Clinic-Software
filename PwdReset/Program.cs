using System;
using BCrypt.Net;

var hash = BCrypt.Net.BCrypt.HashPassword("Admin@123");
Console.WriteLine($"HASH_START:{hash}:HASH_END");
