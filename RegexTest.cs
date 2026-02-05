using System;
using System.Text.RegularExpressions;

public class Program
{
    public static void Main()
    {
        string uid = "d6gLLggG0ki4k-uhj8zxag";
        string pattern = @"_?[A-Za-z0-9_-]{20,24}";
        Console.WriteLine(string.Format("Testing UID: {0}", uid));
        Console.WriteLine(string.Format("Pattern: {0}", pattern));
        
        Regex regex = new Regex(pattern);
        Match match = regex.Match(uid);
        
        if (match.Success)
        {
            Console.WriteLine(string.Format("Match found: '{0}'", match.Value));
            Console.WriteLine(string.Format("Length: {0}", match.Value.Length));
        }
        else
        {
            Console.WriteLine("No match found.");
        }

        string path = "/MenuManager/CharacterUIs/PlayerChar d6gLLggG0ki4k-uhj8zxag UI";
        string normalized = regex.Replace(path, "[CHARACTER]");
        Console.WriteLine(string.Format("Normalized path: {0}", normalized));
    }
}
