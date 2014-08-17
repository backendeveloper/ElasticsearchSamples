﻿using System.Text.RegularExpressions;
using Nest;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MoviesSample
{
    class Program
    {
        private static ElasticClient _client;

        static void Main(string[] args)
        {
            Uri node = new Uri("http://localhost:9200");
            ConnectionSettings settings = new ConnectionSettings(node, defaultIndex: "movies-test");
            _client = new ElasticClient(settings);

            const string filePath = @"C:\Dev\imdb.json\imdb.json";
            using (Stream fileStream = File.OpenRead(filePath))
            using (StreamReader reader = new StreamReader(fileStream))
            using (JsonTextReader jsonReader = new JsonTextReader(reader))
            {
                IDictionary<string, object> values = new Dictionary<string, object>();
                while (jsonReader.Read())
                {
                    JsonToken tokenType = jsonReader.TokenType;
                    if (tokenType == JsonToken.StartArray || tokenType == JsonToken.StartObject || tokenType == JsonToken.StartConstructor)
                    {
                        continue;
                    }

                    if (tokenType == JsonToken.EndArray || tokenType == JsonToken.EndConstructor)
                    {
                        continue;
                    }

                    if (tokenType == JsonToken.EndObject)
                    {
                        WriteToEngine(values);
                        values.Clear();
                        continue;
                    }

                    object tokenValue = jsonReader.Value;
                    if (tokenType == JsonToken.PropertyName)
                    {
                        values.Add(tokenValue.ToString(), null);
                    }
                    else
                    {
                        KeyValuePair<string, object> lastObject = values.Last();
                        values[lastObject.Key] = tokenValue;
                    }
                }
            }
        }

        static void WriteToEngine(IDictionary<string, object> values)
        {
            // If contains rating, this is a movie.
            if (values.ContainsKey("rating"))
            {
                Movie movie;
                try
                {
                    movie = new Movie(values);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unable to create a Movie object. Skipping. Values: {0}", values.ToPrettyString());
                    movie = null;
                }

                if (movie != null)
                {
                    IIndexResponse response = _client.Index(movie, i => i.Index("movies-test"));
                    Console.WriteLine("added one Movie: {0}", response.Id);
                }
            }
            else
            {
                // This is an actor
                // _client.Index(, i => i.Index("movies-test").Type("actor"));
            }
        }
    }

    public class Movie
    {
        /*
          
          {
            "name": "Felicity",
            "url": "/title/tt0578654/",
            "image": "http://ia.media-imdb.com/images/M/MV5BMjA5OTgyMDE3Nl5BMl5BanBnXkFtZTcwNjE1NzcyMQ@@._V1._SX54_CR0,0,54,74_.jpg",
            "rating": 7.6,
            "year": "(1998 TV Series)",
            "nb_voters": 14,
            "episode": "And to All a Good Night",
            "rank": 35973
          }
         
          {
            "name": "The Shawshank Redemption",
            "url": "/title/tt0111161/",
            "image": "http://ia.media-imdb.com/images/M/MV5BMTc3NjM4MTY3MV5BMl5BanBnXkFtZTcwODk4Mzg3OA@@._V1._SX54_CR0,0,54,74_.jpg",
            "rating": 9.3,
            "year": "(1994)",
            "nb_voters": 1010572,
            "rank": 1
          }
         
         */

        private static readonly Regex Regex = new Regex(@"^.*?\([^\d]*(\d+)[^\d]*\).*$", RegexOptions.Compiled);

        public Movie(IDictionary<string, object> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException("values");
            }

            Name = (string)values["name"];
            Url = (string)values["url"];
            Image = (string)values["image"];
            Rating = (double)values["rating"];
            Year = int.Parse(Regex.Match((string)values["year"]).Groups[1].ToString());
            NbVoters = (Int64)values["nb_voters"];
            Rank = (Int64)values["rank"];
        }

        public string Name { get; set; }
        public string Url { get; set; }
        public string Image { get; set; }
        public double Rating { get; set; }
        public int Year { get; set; }
        public string Episode { get; set; }
        public Int64 NbVoters { get; set; }
        public Int64 Rank { get; set; }
    }

    public class Actor
    {
        /*
          {
            "name": "Devon Sorvari",
            "url": "/name/nm0815155/",
            "rank": 21724,
            "image": "http://ia.media-imdb.com/images/M/MV5BNzM3NTE5NDE0MF5BMl5BanBnXkFtZTcwNTkzNjMxMw@@._V1._SY74_CR19,0,54,74_.jpg"
          }         
         */
    }

    public static class DictionaryExtensions
    {
        public static string ToPrettyString<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
        {
            return "{" + string.Join(",", dictionary.Select(kv => kv.Key.ToString() + "=" + kv.Value.ToString()).ToArray()) + "}";
        }
    }
}