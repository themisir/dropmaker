using CommandLine;
using Serilog;
using ShellProgressBar;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Dropmaker
{
    class Program
    {
        public class Options
        {
            [Option('w', "watermark", Required = false, HelpText = "Add watermark to output image.")]
            public string Watermark { get; set; }

            [Option('i', "input", Required = true, HelpText = "Input directory that contains images.")]
            public string Input { get; set; }

            [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
            public bool Verbose { get; set; }

            [Option('o', "output", Required = true, HelpText = "Directory to save converted files.")]
            public string Output { get; set; }

            [Option('q', "quality", Required = false, Default = 80, HelpText = "Json file quality (max: 100).")]
            public int Quality { get; set; }

            [Option('r', "resize", Required = false, HelpText = "Resize and crop the image to fit required size.\n" +
                "n% - Scale down image size to 70 percents of original size\n" +
                "mode:size - Resize image to given size using given resizing mode. Width and height values are separated by x symbol.")]
            public string Resize { get; set; }

            [Option('t', "threads", Required = false, Default = 2, HelpText = "How much threads required to seperate processing tasks.")]
            public int Threads { get; set; }
        }

        public class State
        {
            public string Path { get; set; }
            public string NewPath { get; set; }
            public ProgressBar ProgressBar { get; set; }
        }

        static void Main(string[] args)
        {
            Configuration.Default.ImageFormatsManager.AddImageFormat(JpegFormat.Instance);
            Configuration.Default.ImageFormatsManager.AddImageFormat(PngFormat.Instance);
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(o =>
                {
                    var program = new Program();

                    if (!string.IsNullOrEmpty(o.Watermark))
                    {
                        program.AddWatermark(o.Watermark);
                    }

                    if (!string.IsNullOrEmpty(o.Resize))
                    {
                        program.AddResize(o.Resize);
                    }

                    program.ConfigureQuality(o.Quality);
                    program.ConfigureThreads(o.Threads);

                    program.Run(o.Input, o.Output);
                });

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File("dropmaker.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();
        }

        private Image watermark;
        private Action<IImageProcessingContext> scaler;
        private TaskManager<State> taskManager;
        private JpegEncoder jpegEncoder;

        public void AddWatermark(string path)
        {
            watermark = Image.Load(path);
        }

        public void AddResize(string scale)
        {
            if (scale.EndsWith('%'))
            {
                float p = float.Parse(scale[0..^1], CultureInfo.InvariantCulture);

                scaler = (ctx) =>
                {
                    Size size = ctx.GetCurrentSize();
                    ctx.Resize(
                        Convert.ToInt32(size.Width * p / 100),
                        Convert.ToInt32(size.Height * p / 100));
                };
            }
            else
            if (scale.Contains(':'))
            {
                string[] pairs = scale.Split(':');

                if (pairs.Length != 2)
                {
                    throw new ArgumentException("Paired scale parameter doesn't contains any value.", nameof(scale));
                }

                float widthF, heightF;

                if (pairs[1].Contains('x'))
                {
                    float[] wh = pairs[1]
                        .Split('x')
                        .Select(s => float.Parse(s, CultureInfo.InvariantCulture))
                        .ToArray();

                    widthF = wh[0];
                    heightF = wh[1];
                }
                else
                {
                    float corner = float.Parse(pairs[1], CultureInfo.InvariantCulture);
                    widthF = corner;
                    heightF = corner;
                }

                int width = Convert.ToInt32(widthF),
                    height = Convert.ToInt32(heightF);

                switch (pairs[0])
                {
                    case "contain":
                    case "cnt":
                        scaler = (ctx) =>
                        {
                            ctx.Resize(new ResizeOptions
                            {
                                Size = new Size(width, height),
                                Mode = ResizeMode.BoxPad
                            });
                        };
                        break;

                    case "contain_down":
                    case "cntd":
                        scaler = (ctx) =>
                        {
                            ctx.Resize(new ResizeOptions
                            {
                                Size = new Size(width, height),
                                Mode = ResizeMode.Pad
                            });
                        };
                        break;

                    case "crop":
                        scaler = (ctx) =>
                        {
                            ctx.Resize(new ResizeOptions
                            {
                                Size = new Size(width, height),
                                Mode = ResizeMode.Crop
                            });
                        };
                        break;

                    case "stretch":
                    case "sch":
                        scaler = (ctx) =>
                        {
                            ctx.Resize(new ResizeOptions
                            {
                                Size = new Size(width, height),
                                Mode = ResizeMode.Stretch
                            });
                        };
                        break;

                    case "cover":
                    case "cvr":
                        scaler = (ctx) =>
                        {
                            ctx.Resize(new ResizeOptions
                            {
                                Size = new Size(width, height),
                                Mode = ResizeMode.Max
                            });
                        };
                        break;

                    case "min":
                        scaler = (ctx) =>
                        {
                            ctx.Resize(new ResizeOptions
                            {
                                Size = new Size(width, height),
                                Mode = ResizeMode.Min
                            });
                        };
                        break;

                    default:
                        throw new NotSupportedException(string.Format("{0} is not supported resizing mode", pairs[0]));
                }
            }
            else
            if (scale.Contains('x'))
            {
                int[] wh = scale
                        .Split('x')
                        .Select(s => int.Parse(s, CultureInfo.InvariantCulture))
                        .ToArray();

                scaler = (ctx) =>
                {
                    ctx.Resize(wh[0], wh[1]);
                };
            }
            else
            {
                int wh = int.Parse(scale);

                scaler = (ctx) =>
                {
                    ctx.Resize(wh, wh);
                };
            }
        }

        public void ConfigureThreads(int threads)
        {
            taskManager = new TaskManager<State>(threads, RunOnce);
        }

        public void ConfigureQuality(int quality)
        {
            jpegEncoder = new JpegEncoder
            {
                Quality = quality
            };
        }

        public void Run(string directory, string output)
        {
            var files = Directory.EnumerateFiles(directory).ToArray();

            using var pbar = new ProgressBar(files.Length, "Processing...", new ProgressBarOptions
            {
                ForegroundColor = ConsoleColor.Yellow,
                BackgroundColor = ConsoleColor.DarkYellow,
                ProgressCharacter = '─'
            });

            foreach (string path in files)
            {
                string newPath = Path.Combine(output, Path.GetFileNameWithoutExtension(path) + ".jpg");

                taskManager.Add(new State
                {
                    Path = path,
                    NewPath = newPath,
                    ProgressBar = pbar,
                });
            }

            taskManager.Run();
        }

        public void RunOnce(State state)
        {
            state.ProgressBar.Tick(string.Format("Processing {0}...", state.Path));

            Log.Debug("Mutating {0} into {1}", state.Path, state.NewPath);

            try
            {
                Image image = Image.Load(state.Path);

                image.Mutate(ctx =>
                {
                    ctx.BackgroundColor(Color.White);

                    scaler?.Invoke(ctx);

                    if (watermark != null)
                    {
                        Size size = ctx.GetCurrentSize();
                        int min = Math.Min(size.Width, size.Height);

                        Image resizedMark = watermark.Clone(ctx =>
                        {
                            ctx.Resize(min, min);
                        });

                        ctx.DrawImage(resizedMark, new Point((size.Width - min) / 2, (size.Height - min) / 2), 1);
                    }
                });

                using Stream fs = File.Create(state.NewPath);
                image.SaveAsJpeg(fs, jpegEncoder);
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to mutate {0}", state.Path);
            }
        }
    }
}
