using System.Globalization;

namespace GeoDashSequencer;

public static class SequenceTextFile
{
    public static void Save(string path, IEnumerable<SequenceStep> steps)
    {
        using StreamWriter writer = new(path);
        WriteSteps(writer, steps, 0);
    }

    public static List<SequenceStep> Load(string path)
    {
        string[] lines = File.ReadAllLines(path);
        int index = 0;
        return ReadSteps(lines, ref index, 0);
    }

    private static void WriteSteps(StreamWriter writer, IEnumerable<SequenceStep> steps, int depth)
    {
        string prefix = new('\t', depth);
        foreach (SequenceStep step in steps)
        {
            switch (step)
            {
                case DelayStep delay:
                    writer.WriteLine($"{prefix}DELAY {delay.Milliseconds}");
                    break;
                case KeyboardCommandStep command:
                    writer.WriteLine($"{prefix}KEY {command.Key}");
                    break;
                case MouseCommandStep command:
                    writer.WriteLine($"{prefix}MOUSE {command.Button} {command.Action}");
                    break;
                case LoopStep loop:
                    writer.WriteLine($"{prefix}LOOP {loop.Repetitions}");
                    WriteSteps(writer, loop.Steps, depth + 1);
                    break;
            }
        }
    }

    private static List<SequenceStep> ReadSteps(string[] lines, ref int index, int depth)
    {
        List<SequenceStep> steps = [];
        while (index < lines.Length)
        {
            string line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                index++;
                continue;
            }

            int lineDepth = line.TakeWhile(character => character == '\t').Count();
            if (lineDepth < depth)
                break;
            if (lineDepth > depth)
                throw new FormatException($"Line {index + 1}: unexpected indentation.");

            string[] parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            SequenceStep step = parts[0].ToUpperInvariant() switch
            {
                "DELAY" when parts.Length == 2 => new DelayStep { Milliseconds = ParseInt(parts[1], index) },
                "KEY" when parts.Length == 2 => new KeyboardCommandStep { Key = ParseEnum<Keys>(parts[1], index) },
                "MOUSE" when parts.Length == 3 => new MouseCommandStep
                {
                    Button = ParseEnum<MouseButton>(parts[1], index),
                    Action = ParseEnum<MouseAction>(parts[2], index)
                },
                "LOOP" when parts.Length == 2 => new LoopStep { Repetitions = ParseInt(parts[1], index) },
                _ => throw new FormatException($"Line {index + 1}: invalid command.")
            };

            index++;
            if (step is LoopStep loop)
                loop.Steps.AddRange(ReadSteps(lines, ref index, depth + 1));
            steps.Add(step);
        }
        return steps;
    }

    private static int ParseInt(string value, int index)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) || result < 0)
            throw new FormatException($"Line {index + 1}: expected a non-negative number.");
        return result;
    }

    private static T ParseEnum<T>(string value, int index) where T : struct, Enum
    {
        if (!Enum.TryParse(value, true, out T result))
            throw new FormatException($"Line {index + 1}: invalid {typeof(T).Name}.");
        return result;
    }
}