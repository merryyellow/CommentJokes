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
            jokes = File.ReadAllLines(@"Jokes.txt");
            // jokes are fetched from https://github.com/faiyaz26/one-liner-joke/blob/master/jokes.json
        }

        public static string TellAJoke() => jokes[random.Next(jokes.Length)];
    }
}
