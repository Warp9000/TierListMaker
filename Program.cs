using SixLabors.Fonts;
using SixLabors.ImageSharp.Drawing.Processing;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace TierListMaker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length < 1 || !File.Exists(args[0]))
            {
                Console.WriteLine("File not found. Usage: TierListMaker.exe <json file>");
                return;
            }

            var list = new TierList();
            try
            {
                list.list = JsonConvert.DeserializeObject<Dictionary<string, TierList.Row>>(File.ReadAllText(args[0]))!;
            }
            catch (Exception e)
            {
                Console.WriteLine("Invalid JSON file. " + e.Message);
                return;
            }
            if (list.list == null || list.list.Count == 0)
            {
                Console.WriteLine("Invalid JSON file. (null or empty)");
                return;
            }

            list.Render(Path.GetFileNameWithoutExtension(args[0]) + ".png");
            Console.WriteLine("Done. -> " + Path.GetFileNameWithoutExtension(args[0]) + ".png");
        }
    }

    public class TierList
    {
        public class Row
        {
            public string color = "#000000";
            public string[] items = new string[0];
        }
        public Dictionary<string, Row> list = new();
        private int columns = 0;
        public void Render(string outPath)
        {
            columns = list.Values.Max(x => x.items.Length + 1);
            var Tiers = list.Select(x => (x.Key, Color.ParseHex(x.Value.color))).ToArray();
            var image = GenBG(Tiers);
            var font = SystemFonts.CreateFont("Arial", 32);
            for (int i = 0; i < Tiers.Length; i++)
            {
                var tier = Tiers[i];
                var tierList = list[tier.Key];
                for (int j = 0; j < tierList.items.Length; j++)
                {
                    var path = tierList.items[j];
                    if (path.StartsWith("http"))
                    {
                        var hash = BitConverter.ToString(SHA256.HashData(Encoding.UTF8.GetBytes(path))).Replace("-", "");
                        Directory.CreateDirectory("temp");
                        if (!File.Exists($"temp/{hash}.png"))
                        {
                            Console.WriteLine($"Downloading {path}");
                            using var client = new HttpClient();
                            var bytes = client.GetByteArrayAsync(path).Result;
                            using var ms = new MemoryStream(bytes);
                            File.WriteAllBytes($"temp/{hash}.png", bytes);
                        }
                        path = $"temp/{hash}.png";
                    }
                    if (!File.Exists(path))
                    {
                        Console.WriteLine($"File not found: {path}");
                        continue;
                    }
                    var img = Image.Load(path);
                    img.Mutate(x => x.Resize(128, 128));
                    image.Mutate(x => x.DrawImage(img, new Point(128 * (j + 1), 128 * i), 1));
                }
            }
            GenLines(image, Tiers.Length);
            image.Save(outPath);
        }
        private Image<Rgba32> GenBG((string name, Color color)[] Tiers)
        {
            int rows = Tiers.Length;
            Image<Rgba32> Img = new(128 * columns, 128 * rows);
            Img.Mutate(x => x.BackgroundColor(Color.ParseHex("#404040")));
            var font = SystemFonts.CreateFont("Arial", 32);
            for (int i = 0; i < rows; i++)
            {
                Img.Mutate(x => x.Fill(Tiers[i].color, new RectangleF(0, 128 * i, 128, 128)));
                var text = Tiers[i].name;
                var size = TextMeasurer.Measure(text, new TextOptions(font));
                var location = new PointF(64 - size.Width / 2, 64 - size.Height / 2 + 128 * i);
                Img.Mutate(x => x.DrawText(text, font, Color.Black, location));
            }
            return Img;
        }
        private Image<Rgba32> GenLines(Image<Rgba32> Img, int rows)
        {
            for (int i = 0; i < rows - 1; i++)
            {
                Img.Mutate(x => x.DrawLines(Color.Black, 2, new PointF(0, 128 * (i + 1)), new PointF(128 * columns, 128 * (i + 1))));
            }
            Img.Mutate(x => x.DrawLines(Color.Black, 2, new PointF(128, 0), new PointF(128, 128 * rows)));
            return Img;
        }
    }
}