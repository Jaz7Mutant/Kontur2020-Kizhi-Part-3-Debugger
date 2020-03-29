using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KizhiPart3
{
    public class Debugger
    {
        private const string VariableNotFoundError = "Переменная отсутствует в памяти";
        private static Dictionary<string, Action<string[]>> kizhiCommands;
        private static Dictionary<string, Action<string>> interpreterCommands;

        private readonly TextWriter writer;
        private readonly List<string> codeLines;
        private readonly Dictionary<string, int> functions;
        private readonly HashSet<int> breakpoints;
        private bool settingCodeMode;

        private readonly Dictionary<string, Variable> variables;
        private readonly Stack<CallStackEntry> callStack;
        private int currentLineIndex;
        private int lastBrakedLine;

        public Debugger(TextWriter writer)
        {
            kizhiCommands = new Dictionary<string, Action<string[]>>
            {
                {"set", SetVariable},
                {"sub", Sub},
                {"print", Print},
                {"rem", Remove},
                {"call", CallFunction}
            };
            interpreterCommands = new Dictionary<string, Action<string>>
            {
                {"set code", SetCode},
                {"end set code", EndSetCode},
                {"run", RunProgram},
                {"add break", AddBreakpoint},
                {"step over", StepOver},
                {"step", Step},
                {"print mem", PrintMemory},
                {"print trace", PrintStacktrace}
            };

            this.writer = writer;
            codeLines = new List<string>();
            functions = new Dictionary<string, int>();
            breakpoints = new HashSet<int>();
            variables = new Dictionary<string, Variable>();
            callStack = new Stack<CallStackEntry>();
            currentLineIndex = 0;
            lastBrakedLine = -1;
        }

        public void ExecuteLine(string command)
        {
            if (command == null || command.Equals(""))
            {
                return;
            }

            var interpreterCommand = interpreterCommands.Keys.FirstOrDefault(command.StartsWith);
            if (interpreterCommand != null)
            {
                interpreterCommands[interpreterCommand].Invoke(command);
                return;
            }

            if (settingCodeMode)
            {
                ParseCodeLines(command.Split('\n'));
                return;
            }

            ExecuteKizhiCommand(command);
        }

        private void ParseCodeLines(string[] lines)
        {
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("def", StringComparison.Ordinal))
                {
                    functions.Add(lines[i].Split()[1], i);
                }

                codeLines.Add(lines[i]);
            }
        }

        private void SetCode(string _) => settingCodeMode = true;

        private void EndSetCode(string _) => settingCodeMode = false;

        private void AddBreakpoint(string command) => breakpoints.Add(int.Parse(command.Split()[2]));

        private void PrintMemory(string _)
        {
            foreach (var variable in variables)
            {
                writer.WriteLine($"{variable.Key} {variable.Value.Value} {variable.Value.LastModifiedLineIndex}");
            }
        }

        private void PrintStacktrace(string _)
        {
            foreach (var callStackEntry in callStack)
            {
                writer.WriteLine($"{callStackEntry.CallLineIndex} {callStackEntry.FunctionName}");
            }
        }

        private void ExecuteKizhiCommand(string command)
        {
            var parsedCommand = command.Trim().Split();
            if (kizhiCommands.ContainsKey(parsedCommand[0]))
            {
                kizhiCommands[parsedCommand[0]].Invoke(parsedCommand);
            }
            else
            {
                throw new ArgumentException("Wrong command", command);
            }
        }

        private void RunProgram(string _)
        {
            while (true)
            {
                if (breakpoints.Contains(currentLineIndex) && currentLineIndex != lastBrakedLine)
                {
                    lastBrakedLine = currentLineIndex;
                    return;
                }

                Step(_);
                if (currentLineIndex == 0) // Program run has been completed
                {
                    return;
                }
            }
        }

        private void Step(string _)
        {
            SkipFunctionLines();
            if (currentLineIndex >= codeLines.Count)
            {
                Clear();
                return;
            }

            ExecuteKizhiCommand(codeLines[currentLineIndex]);
            GetNextLineToExecute();
        }

        private void GetNextLineToExecute()
        {
            currentLineIndex++;

            if (currentLineIndex >= codeLines.Count
                || char.IsWhiteSpace(codeLines[currentLineIndex - 1][0])
                && !char.IsWhiteSpace(codeLines[currentLineIndex][0]))
            {
                if (callStack.Count != 0)
                {
                    currentLineIndex = callStack.Pop().CallLineIndex + 1;
                }

                if (currentLineIndex >= codeLines.Count)
                {
                    Clear();
                }
            }
        }

        private void SkipFunctionLines()
        {
            if (codeLines[currentLineIndex].StartsWith("def"))
            {
                while (currentLineIndex < codeLines.Count
                       && (char.IsWhiteSpace(codeLines[currentLineIndex][0])
                           || codeLines[currentLineIndex].StartsWith("def")))
                {
                    currentLineIndex++;
                }
            }
        }

        private void StepOver(string _)
        {
            var currentStackSize = callStack.Count;
            while (true)
            {
                Step(_);
                if (callStack.Count == currentStackSize)
                {
                    return;
                }
            }
        }

        private void Clear()
        {
            variables.Clear();
            callStack.Clear();
            currentLineIndex = 0;
            lastBrakedLine = -1;
        }

        private void SetVariable(string[] args)
        {
            var name = args[1];
            if (int.TryParse(args[2], out var value))
            {
                if (value > 0)
                {
                    variables[name] = new Variable(value, currentLineIndex);
                }
                else
                {
                    throw new ArgumentOutOfRangeException(value.ToString(), "Value should be greater than 0");
                }
            }
        }

        private void Sub(string[] args)
        {
            var name = args[1];
            if (int.TryParse(args[2], out var value))
            {
                if (variables.ContainsKey(name))
                {
                    if (value < 0)
                    {
                        throw new ArgumentOutOfRangeException(value.ToString(), "Value should be greater than 0");
                    }

                    if (variables[name].Value < value)
                    {
                        throw new ArithmeticException("Operation result should be greater than 0");
                    }

                    variables[name].Value -= value;
                    variables[name].LastModifiedLineIndex = currentLineIndex;
                }
                else
                {
                    writer.WriteLine(VariableNotFoundError);
                }
            }
        }

        private void Print(string[] args)
        {
            var name = args[1];
            if (variables.ContainsKey(name))
            {
                writer.WriteLine(variables[name].Value);
            }
            else
            {
                writer.WriteLine(VariableNotFoundError);
            }
        }

        private void Remove(string[] args)
        {
            var name = args[1];
            if (variables.ContainsKey(name))
            {
                variables.Remove(name);
            }
            else
            {
                writer.WriteLine(VariableNotFoundError);
            }
        }

        private void CallFunction(string[] args)
        {
            var name = args[1];
            if (functions.ContainsKey(name))
            {
                callStack.Push(new CallStackEntry(name, currentLineIndex));
                currentLineIndex = functions[name];
            }
            else
            {
                throw new ArgumentException("This function doesn't exist", name);
            }
        }

        private class Variable
        {
            public int Value;
            public int LastModifiedLineIndex;

            public Variable(int value, int lastModifiedLineIndex)
            {
                Value = value;
                LastModifiedLineIndex = lastModifiedLineIndex;
            }
        }

        private class CallStackEntry
        {
            public readonly string FunctionName;
            public readonly int CallLineIndex;

            public CallStackEntry(string functionName, int callLineIndex)
            {
                FunctionName = functionName;
                CallLineIndex = callLineIndex;
            }
        }
    }
}