using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


class AddEmUp
{
    static void Main(string[] args)
    {
        Console.WriteLine("Starting ♠♥♣♦ Add 'Em Up...");
        new Game().StartGame(args);
        Console.WriteLine("Game Completed.");
        Console.ReadKey();
    }
}

internal class Game
{
    private const string ErrorMessage = "ERROR";
    private const int MaxPlayerCardCount = 5;
    private static string CurrentDirectory = Directory.GetCurrentDirectory();
    private Dictionary<string, string[]> PlayerHands = new Dictionary<string, string[]>(MaxPlayerCardCount);
    private ConcurrentDictionary<string, int> PlayerScores = new ConcurrentDictionary<string, int>();
    private ConcurrentDictionary<string, int> PlayerSuitScores = new ConcurrentDictionary<string, int>();

    private static Dictionary<string, int> numberCardValues = new Dictionary<string, int>
    {
        {"J", 11},
        {"Q", 12},
        {"K", 13},
        {"A", 1}
    };
    private static Dictionary<string, int> suitValues = new Dictionary<string, int>
    {
        {"S", 4},
        {"H", 3},
        {"D", 2},
        {"C", 1}
    };
    private static String[] validCardValues = {
        "A", "2", "3", "4", "5", "6", "7", "8", "9", "10",
        "J", "Q", "K" ,
        "S", "H", "D","C"
    };

    #region Core Game Logic 
    public void StartGame(string[] args)
    {
        (string inputFilePath, string outputFilePath, bool isValid) = ValidateInputAsync(args);

        try
        {
            // Validate input
            if (!isValid)
            {
                PrintResults(outputFilePath, ErrorMessage);
                return;
            }

            // Tally scores: find a clear winner 
            CalculatePlayerScores();
            var highestPlayerScores = FindHighestPlayerScores(ref PlayerScores);
            if (highestPlayerScores.Count == 1)
            {
                var winner = highestPlayerScores.FirstOrDefault();
                PrintResults(outputFilePath, $"{winner.Key}: {winner.Value}");
                return;
            }

            // No clear winner: tally suit scores
            CalculateSuitScores(highestPlayerScores);
            var highestSuitScores = FindHighestPlayerScores(ref PlayerSuitScores);
            if (highestSuitScores.Count == 1)
            {
                var winner = highestSuitScores.FirstOrDefault();
                PrintResults(outputFilePath, $"{winner.Key}: {winner.Value}");
                return;
            }

            // No Tie break: multiple winners
            var resultString = new StringBuilder();
            for (int i = 0; i < highestSuitScores.Count; i++)
            {
                resultString.Append($"{highestSuitScores.ToArray()[i].Key}");

                _ = highestSuitScores.Count > i + 1 ?
                    resultString.Append(',') :
                    resultString.Append($":{highestSuitScores.First().Value}");
            }

            PrintResults(outputFilePath, resultString.ToString());
        }
        catch (Exception ex)
        {
            Console.WriteLine("An exception was thrown while running the game: " + ex.Message);
            PrintResults(outputFilePath, ErrorMessage);
        }
    }

    #endregion

    #region Validation 

    private (string inputFilePath, string outputFilePath, bool validationResult) ValidateInputAsync(string[] input)
    {
        var inputFilePath = String.Empty;
        var outputFilePath = String.Empty;
        var isValid = true;

        if (input.Length != 4)
        {
            return (inputFilePath, outputFilePath, false);
        }

        // get input & output paths 
        int inFlagIndex = Array.IndexOf(input, input.Where(x => x.Contains("--in")).First());
        int outFlagIndex = Array.IndexOf(input, input.Where(x => x.Contains("--out")).First());

        inputFilePath = input[inFlagIndex + 1];
        outputFilePath = input[outFlagIndex + 1];

        //Validate input & output paths were provided
        if (string.IsNullOrEmpty(inputFilePath) || string.IsNullOrEmpty(outputFilePath))
            return (inputFilePath, outputFilePath, false);

        if (!IsPathFullyQualified(inputFilePath))
            inputFilePath = CurrentDirectory + Path.Combine(inputFilePath);

        if (!IsPathFullyQualified(outputFilePath))
            outputFilePath = CurrentDirectory + Path.Combine(outputFilePath);

        // validate if files exist 
        if (!File.Exists(Path.GetFullPath(inputFilePath)) ||
            !File.Exists(Path.GetFullPath(outputFilePath)))
            isValid = false;

        // Validate input file content
        string[] inputLines = File.ReadAllLines(Path.Combine(CurrentDirectory, inputFilePath));

        if (inputLines.Length != MaxPlayerCardCount || inputLines.Distinct().Count() != MaxPlayerCardCount)
            isValid = false;

        foreach (string line in inputLines)
        {
            var playerName = line.Split(':')[0];
            var cards = line.Split(':')[1].Split(',');

            if (cards.Length != MaxPlayerCardCount || PlayerHands.ContainsValue(cards))
                isValid = false;

            var validCards = cards.Where(x =>
            {
                var value = x.Substring(0, x.Length == 2 ? 1 : 2);
                var suit = x.Substring(x.Length == 2 ? 1 : 2, 1);

                return validCardValues.Contains(value) &&
                       validCardValues.Contains(suit) &&
                       x.Length <= 3;

            }).ToArray().Length;

            if (validCards != MaxPlayerCardCount)
                isValid = false;

            PlayerHands.Add(playerName, cards);
        }

        return (inputFilePath, outputFilePath, isValid);
    }

    #endregion

    #region Helper functions 

    private void CalculatePlayerScores()
    {
        Parallel.ForEach(PlayerHands, (src, state) =>
        {
            var score = src.Value.Select(card =>
            {
                var cardCharacter = card.Substring(0, 1).ToCharArray().First();
                int cardValue;

                if (Char.IsDigit(cardCharacter))
                    return (int)cardCharacter;

                numberCardValues.TryGetValue(cardCharacter.ToString(), out cardValue);

                return cardValue;

            }).Sum();

            PlayerScores.TryAdd(src.Key, score);
        });
    }

    private static Dictionary<string, int> FindHighestPlayerScores(ref ConcurrentDictionary<string, int> playerScores)
    {
        Dictionary<string, int> Finalists = new Dictionary<string, int>();

        foreach (var score in playerScores)
        {
            if (Finalists.Count == 0)
            {
                Finalists.Add(score.Key, score.Value);
            }
            else
            {
                var finalistArray = Finalists.ToArray();

                for (int i = 0; i < Finalists.Count; i++)
                {
                    var highScore = finalistArray[i];

                    if (score.Value > highScore.Value)
                    {
                        Finalists.Remove(highScore.Key);
                        Finalists.Add(score.Key, score.Value);
                        break;
                    }
                    else if (score.Value == highScore.Value)
                    {
                        Finalists.Add(score.Key, score.Value);
                        break;
                    }
                }
            }
        }

        return Finalists;
    }

    private void CalculateSuitScores(Dictionary<string, int> highScores)
    {
        Dictionary<string, string[]> finalistHands = PlayerHands.Where(hand => highScores.ContainsKey(hand.Key)).ToDictionary(x => x.Key, x => x.Value);

        Parallel.ForEach(finalistHands, (src, state) =>
        {
            var score = src.Value.Select(card =>
            {
                var cardCharacter = card.Substring(1, 1);
                int cardValue;

                suitValues.TryGetValue(cardCharacter.ToUpper(), out cardValue);

                return cardValue;

            }).Sum();

            PlayerSuitScores.TryAdd(src.Key, score);
        });
    }

    private static bool IsPathFullyQualified(string path)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));
        if (path.Length < 2) return false; //There is no way to specify a fixed path with one character (or less).
        if (path.Length == 2 && IsValidDriveChar(path[0]) && path[1] == System.IO.Path.VolumeSeparatorChar) return true; //Drive Root C:
        if (path.Length >= 3 && IsValidDriveChar(path[0]) && path[1] == System.IO.Path.VolumeSeparatorChar && IsDirectorySeperator(path[2])) return true; //Check for standard paths. C:\
        if (path.Length >= 3 && IsDirectorySeperator(path[0]) && IsDirectorySeperator(path[1])) return true; //This is start of a UNC path
        return false;
    }

    private static bool IsDirectorySeperator(char c) => c == System.IO.Path.DirectorySeparatorChar | c == System.IO.Path.AltDirectorySeparatorChar;

    private static bool IsValidDriveChar(char c) => c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z';

    private static void PrintResults(string relativePath, string resultString)
    {
        var fullPath = Path.Combine(CurrentDirectory, relativePath);

        File.WriteAllText(fullPath, resultString);
    }
    #endregion
}