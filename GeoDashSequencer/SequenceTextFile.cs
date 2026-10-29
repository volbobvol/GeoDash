using System.Globalization;

namespace GeoDashSequencer;

public static class SequenceTextFile
{
    public static void Save(string path, IEnumerable<SequenceStep> steps)
    {
        using StreamWriter writer = new(path);
        WriteSteps(writer, SequenceValidator.CreateExecutionSnapshot(steps), 0);
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
                case WaitForPixelChangeStep wait:
                    writer.WriteLine($"{prefix}PIXEL_CHANGE {wait.ScreenX} {wait.ScreenY} {wait.PollMilliseconds}");
                    break;
                case KeyboardCommandStep command:
                    writer.WriteLine($"{prefix}KEY {KeyboardShortcut.Format(command.Key)}");
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
                "PIXEL_CHANGE" when parts.Length == 4 => new WaitForPixelChangeStep
                {
                    ScreenX = ParseSignedInt(parts[1], index),
                    ScreenY = ParseSignedInt(parts[2], index),
                    PollMilliseconds = ParsePositiveInt(parts[3], index)
                },
                "KEY" when parts.Length >= 2 => new KeyboardCommandStep { Key = ParseKey(line.Trim(), index) },
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

    private static int ParsePositiveInt(string value, int index)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) || result <= 0)
            throw new FormatException($"Line {index + 1}: expected a positive number.");
        return result;
    }

    private static int ParseSignedInt(string value, int index)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
            throw new FormatException($"Line {index + 1}: expected a screen coordinate.");
        return result;
    }

    private static Keys ParseKey(string line, int index)
    {
        try
        {
            return KeyboardShortcut.Parse(line[3..].Trim());
        }
        catch (FormatException exception)
        {
            throw new FormatException($"Line {index + 1}: {exception.Message}");
        }
    }

    private static T ParseEnum<T>(string value, int index) where T : struct, Enum
    {
        if (!Enum.TryParse(value, true, out T result) || !Enum.IsDefined(result))
            throw new FormatException($"Line {index + 1}: invalid {typeof(T).Name}.");
        return result;
    }
}