using System;
using System.IO;

namespace CommentJokes
{
    public static class Joker
    {
        static string[] jokes;
        static Random random;
        static Joker()
        {
            random = new Random();
            var dllPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var dllDir = Path.GetDirectoryName(dllPath);
            var txtPath = Path.Combine(dllDir, "Jokes.txt");

            //jokes = File.ReadAllLines(@"/Jokes.txt");
            jokes = File.ReadAllLines(txtPath);
            // jokes are fetched from https://github.com/faiyaz26/one-liner-joke/blob/master/jokes.json
        }

        public static string TellAJoke() => jokes[random.Next(jokes.Length)];
    }
}
