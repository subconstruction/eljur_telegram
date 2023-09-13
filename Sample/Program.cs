using System;
using System.Net;
using System.Text;
using System.IO;
using System.Xml;
using System.Data.SqlTypes;
using System.Timers;
using System.Linq;
using System.Numerics;
using System.Xml.Linq;
using System.Threading;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Net.Http;

using Newtonsoft.Json;

using AngleSharp.Dom;
using AngleSharp.Html.Parser;

using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.InlineQueryResults;
using Newtonsoft.Json.Linq;

namespace test
{
    class Program
    {
        static readonly string TelegramToken = ""; // Insert your telegram's bot token here
        static readonly System.Timers.Timer timer = new System.Timers.Timer();
        static readonly TelegramBotClient TG = new TelegramBotClient(TelegramToken);

        static byte[] Encrypt(string plainText, byte[] key, byte[] iv)
        {
            byte[] encrypted;

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(plainText);
                        }
                        encrypted = msEncrypt.ToArray();
                    }
                }
            }

            return encrypted;
        }

        static string Decrypt(byte[] cipherText, byte[] key, byte[] iv)
        {
            string? plaintext = null;

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using (MemoryStream msDecrypt = new MemoryStream(cipherText))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {
                            plaintext = srDecrypt.ReadToEnd();
                        }
                    }
                }
            }

            return plaintext;
        }

        static void Main(string[] args)
        {
            try
            {
                string folderName = "db";
                string path = @"" + folderName;

                try
                {
                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                        Console.WriteLine("Folder created successfully at " + path);
                    }
                    else
                    {
                        Console.WriteLine("Folder already exists at " + path);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("An error occurred: " + e.Message);
                }

                folderName = "cache";
                path = @"db/" + folderName;

                try
                {
                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                        Console.WriteLine("Folder created successfully at " + path);
                    }
                    else
                    {
                        Console.WriteLine("Folder already exists at " + path);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("An error occurred: " + e.Message);
                }

                Console.Title = "Actual Informaiton Updater";

                TG.StartReceiving(Update, Error);

                timer.Interval = 86000;
                timer.Elapsed += Timer_Elapsed;

                timer.Start();
                Console.ReadLine();
            }

            catch (Exception mainException)
            {
                Console.WriteLine("Error in main");
                Console.WriteLine(mainException);
            }
        }

        static async void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            //if (true) return;
            foreach (string file in Directory.EnumerateFiles("db/", "*.txt"))
            {
                Thread.Sleep(1000);
                long chatId = Convert.ToInt64(file.Replace(".txt", "").Replace("db/", ""));
                bool foundContent = System.IO.File.Exists($"db/cache/{chatId}.txt");

                Dictionary<string, object> timeTable = AngleSharpParse(chatId);

                byte[] key_cipher = new byte[32];
                Array.Fill(key_cipher, (byte)0);

                byte[] iv = new byte[16];
                Array.Fill(iv, (byte)0);

                string data = JsonConvert.SerializeObject(timeTable).ToString();
                byte[] encrypted_data = Encrypt(data, key_cipher, iv);

                if (!foundContent)
                {

                    System.IO.File.WriteAllText($"db/cache/{chatId}.txt", Convert.ToBase64String(encrypted_data));
                    continue;
                };

                Dictionary<string, object> last_content = JsonConvert.DeserializeObject<Dictionary<string, object>>(
                    Decrypt(Convert.FromBase64String(System.IO.File.ReadAllText($"db/cache/{chatId}.txt")), key_cipher, iv));

                for (int dayI = 0; dayI < timeTable.Count; dayI++)
                {
                    string[] daysObject = { "Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота", "Воскресенье" };
                    string BuildMessage = $"⚠️ Обновлен день [{daysObject[dayI]}]\n\n\n";
                    bool isChanged = false;

                    Dictionary<string, object>? dayDict;
                    if (timeTable.TryGetValue(dayI.ToString(), out var dayDictValue))
                    {
                        dayDict = dayDictValue as Dictionary<string, object>;
                        if (dayDict != null)
                        {
                            foreach (var key in dayDict)
                            {
                                if (dayDict.TryGetValue(key.Key, out var lessonsDictValue))
                                {
                                    var lessonsDict = lessonsDictValue as Dictionary<string, object>;
                                    if (lessonsDict == null)
                                    {
                                        await TG.SendTextMessageAsync(chatId, "❌ An error occured => NullReferenceException ХАХАХ Кстати BREAKPOINT: 1:1");
                                        break;
                                    }
                                    {
                                        if (lessonsDict.TryGetValue("lessonsDict", out var lessonsDictValue2))
                                        {
                                            var lessonsDict2 = lessonsDictValue2 as Dictionary<string, object>;
                                            if (lessonsDict2 == null)
                                            {
                                                await TG.SendTextMessageAsync(chatId, "❌ An error occured => NullReferenceException ХАХАХ Кстати BREAKPOINT: 2:1");
                                                break;
                                            }
                                            {
                                                string name = lessonsDict2["name"].ToString().Trim();
                                                string ord = lessonsDict2["ord"].ToString().Trim();
                                                string time = lessonsDict2["time"].ToString().Trim();
                                                string homeTask = lessonsDict2["hometask"].ToString().Trim().Replace("\n", "");
                                                string mark = lessonsDict2["mark"].ToString().Trim().Replace(" ", "").Replace("×1", "");
                                                var reply = lessonsDict2["attach_name"].ToString().Split(",");
                                                var replyCtx = lessonsDict2["attach_content"].ToString().Split(",");

                                                Dictionary<string, object> dayDictQ;
                                                if (last_content.TryGetValue(dayI.ToString(), out var dayDictValueQ))
                                                {
                                                    dayDictValueQ = JsonConvert.DeserializeObject<Dictionary<string, object>>(dayDictValueQ.ToString());
                                                    dayDictQ = (Dictionary<string, object>)dayDictValueQ;
                                                    if (dayDictQ != null)
                                                    {
                                                        //foreach (var keyQ in dayDictQ)
                                                        {
                                                            if (dayDictQ.TryGetValue(key.Key, out var lessonsDictValueQ))
                                                            {
                                                                lessonsDictValueQ = JsonConvert.DeserializeObject<Dictionary<string, object>>(lessonsDictValueQ.ToString());
                                                                var lessonsDictQ = lessonsDictValueQ as Dictionary<string, object>;
                                                                if (lessonsDictQ == null)
                                                                {
                                                                    await TG.SendTextMessageAsync(chatId, "❌ An error occured: NullReferenceException ХАХАХ Кстати BREAKPOINT: 1:2");
                                                                    break;
                                                                }
                                                                {
                                                                    if (lessonsDictQ.TryGetValue("lessonsDict", out var lessonsDictValue2Q))
                                                                    {
                                                                        lessonsDictValue2Q = JsonConvert.DeserializeObject<Dictionary<string, object>>(lessonsDictValue2Q.ToString());
                                                                        var lessonsDict2Q = lessonsDictValue2Q as Dictionary<string, object>;
                                                                        if (lessonsDict2Q == null)
                                                                        {
                                                                            await TG.SendTextMessageAsync(chatId, "❌ An error occured: NullReferenceException ХАХАХ Кстати BREAKPOINT: 2:2");
                                                                            break;
                                                                        }
                                                                        {
                                                                            string nameQ = lessonsDict2Q["name"].ToString().Trim();
                                                                            string ordQ = lessonsDict2Q["ord"].ToString().Trim();
                                                                            string timeQ = lessonsDict2Q["time"].ToString().Trim();
                                                                            string homeTaskQ = lessonsDict2Q["hometask"].ToString().Trim().Replace("\n", "");
                                                                            string markQ = lessonsDict2Q["mark"].ToString().Trim().Replace("\n", "").Replace("×1", "");

                                                                            var replyQ = lessonsDict2Q["attach_name"].ToString().Split(",");
                                                                            var replyCtxQ = lessonsDict2Q["attach_content"].ToString().Split(",");

                                                                            foreach (KeyValuePair<string, object> kvp in lessonsDict2Q)
                                                                            {
                                                                                var dictKey = "name";
                                                                                if (lessonsDict2[dictKey] is not null)
                                                                                {

                                                                                    if (lessonsDict2Q[dictKey].ToString() != lessonsDict2[dictKey].ToString() & dictKey == "name")
                                                                                    {
                                                                                        isChanged = true;
                                                                                    }
                                                                                }
                                                                            }

                                                                            if (homeTask != homeTaskQ) await TG.SendTextMessageAsync(chatId, $"💬 {name}: Заданно Д/З -> {homeTask}");
                                                                            if (markQ != mark) await TG.SendTextMessageAsync(chatId, $"💬 {nameQ}: Новая Оценка -> {mark.Replace(markQ, "").Replace("/", "")}");
                                                                            BuildMessage = $"{BuildMessage}🎓 {ord}{lessonsDict2["name"].ToString()} [{time}]\n✍🏻 Д/З: {homeTask}\n📍 Оценка(и): {mark}\n\n";
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        if (isChanged)
                        {
                            var replyMessage = await TG.SendTextMessageAsync(
                                chatId = chatId,
                                text: BuildMessage);
                        }

                        BuildMessage = $"🗓️ {daysObject[dayI]}\n\n\n";
                        isChanged = false;
                    }
                }

                System.IO.File.WriteAllText($"db/cache/{chatId}.txt", Convert.ToBase64String(encrypted_data));
            }
        }

        public static Dictionary<string, object> AngleSharpParse(long id = 123)
        {
            try
            {
                var parser = new HtmlParser();
                var readData = System.IO.File.Exists($"db/{id}.txt");

                byte[] key_cipher = new byte[32];
                Array.Fill(key_cipher, (byte)0);

                byte[] iv = new byte[16];
                Array.Fill(iv, (byte)0);

                bool isExists = System.IO.File.Exists($"db/{id}.txt");

                if (id == 123 || !isExists)
                {
                    TG.SendTextMessageAsync(id, "👤 User is not detected! Authorize using the following syntax:\n\n/auth <username> <password>");
                    return new();
                }

                Userdata parsed_json = JsonConvert.DeserializeObject<Userdata>(System.IO.File.ReadAllText($"db/{id}.txt"));
                var auth_params = new Dictionary<string, string>()
                {
                    { "username" , Decrypt(Convert.FromBase64String(parsed_json.username), key_cipher, iv) },
                    { "password" , Decrypt(Convert.FromBase64String(parsed_json.password), key_cipher, iv) }
                };

                var Journal_Body = GetRequest("https://kip.eljur.ru/?show=home", auth_params).Result.Content.ReadAsStringAsync().Result;
                var document = parser.ParseDocument(Journal_Body);
                int dayCounter = 0;
                int EntriesCount = 0;

                Dictionary<string, object> timeTable = new Dictionary<string, object>()
                {
                };

                foreach (IElement element in document.GetElementsByClassName("dnevnik-day"))
                {
                    IHtmlCollection<IElement> wtf = element.GetElementsByClassName("dnevnik-day__title");
                    IHtmlCollection<IElement> lessonsCollection = element.GetElementsByClassName("dnevnik-lesson");

                    string[] dayTitle = wtf[0].TextContent.Trim().Replace("\n", "").Split(",");

                    string WeekDay = dayTitle[0];
                    string WeekDate = dayTitle[1];

                    // Main Code
                    int LessonCount = 0;

                    Dictionary<string, object> timeTableLesson = new Dictionary<string, object>()
                    {
                    };

                    timeTable.Add(dayCounter.ToString(), timeTableLesson);
                    foreach (IElement Lesson in lessonsCollection)
                    {
                        IElement wtf2 = Lesson.GetElementsByClassName("dnevnik-lesson__number dnevnik-lesson__number--time")[0];
                        IElement wtf3 = Lesson.GetElementsByClassName("js-rt_licey-dnevnik-subject")[0];
                        IElement wtf4 = Lesson.GetElementsByClassName("dnevnik-lesson__time")[0];
                        IHtmlCollection<IElement> wtf5 = Lesson.GetElementsByClassName("dnevnik-lesson__task");
                        IHtmlCollection<IElement> wtf6 = Lesson.GetElementsByClassName("dnevnik-mark");
                        IHtmlCollection<IElement> wtf7 = Lesson.GetElementsByClassName("button button--outline button--purple");

                        string lesson_num = wtf2.TextContent;
                        string lessonMark = "Нет";
                        string lessonName = wtf3.TextContent.Trim().Replace("\n", "");
                        string lessonTime = wtf4.TextContent.Trim().Replace("\n", "");
                        string lessonAttach = "";
                        string lessonAttachContent = "";
                        string lessonHomeTask = "Нет";

                        switch (wtf6.Length)
                        {
                            case 1:
                                lessonMark = wtf6[0].TextContent.Trim().Replace("\n", "");
                                break;


                            case 2:
                                lessonMark = $"{wtf6[0].TextContent.Trim().Replace("\n", "")}/{wtf6[1].TextContent.Trim().Replace("\n", "")}".Trim().Replace("\n", "");
                                break;
                        }

                        switch (wtf7.Length)
                        {
                            case 1:
                                foreach (IElement attachedContent in wtf7)
                                {
                                    lessonAttach = attachedContent.TextContent;
                                    lessonAttachContent = attachedContent.GetAttribute("href");
                                }
                                break;
                            case 0:
                                break;
                            default:
                                foreach (IElement attachedContent in wtf7)
                                {
                                    lessonAttach += "," + attachedContent.TextContent;
                                    lessonAttachContent += "," + attachedContent.GetAttribute("href");
                                }
                                break;
                        }

                        switch (wtf5.Length)
                        {
                            case 1:
                                lessonHomeTask = wtf5[0].TextContent;

                                var href_class = wtf5[0].GetElementsByClassName("b-href external-link");

                                foreach (IElement href_content in href_class)
                                {
                                    string href = href_content.GetAttribute("href");
                                    string href_text = href_content.TextContent;

                                    lessonAttach += $",{href_text}";
                                    lessonAttachContent += $",{href}";
                                }
                                break;

                            default:
                                foreach (var attachedContent in wtf5)
                                {
                                    string content = attachedContent.TextContent;
                                    var href_classes = attachedContent.GetElementsByClassName("b-href external-link");

                                    if (lessonHomeTask == "Не заданно")
                                    {
                                        lessonHomeTask = content;
                                    }
                                    else
                                    {
                                        lessonHomeTask += $"\n {content}";
                                    };

                                    foreach (IElement href_content in href_classes)
                                    {
                                        string href = href_content.GetAttribute("href");
                                        string href_text = href_content.TextContent;

                                        lessonAttach += $",{href_text}";
                                        lessonAttachContent += $",{href}";
                                    }

                                }
                                break;
                        }


                        {
                            lessonMark = lessonMark.Replace(" ", "");

                            int lessonMarkMultIdx = lessonMark.IndexOf("×");
                            if (lessonMarkMultIdx > 0) lessonMark.Remove(lessonMarkMultIdx, 2);
                        }

                        Dictionary<string, object> timeTableObj = new Dictionary<string, object>()
                    {
                        {
                            "lessonsDict", new Dictionary<string, object>()
                            {
                                { "day", WeekDay },
                                { "date", WeekDate },
                                { "ord", lesson_num },
                                { "name", lessonName },
                                { "time", lessonTime },
                                { "hometask", lessonHomeTask },
                                { "mark", lessonMark },
                                { "attach_name", lessonAttach },
                                { "attach_content", lessonAttachContent }
                            }
                        }
                    };

                        timeTableLesson[LessonCount.ToString()] = timeTableObj;
                        LessonCount++;
                        EntriesCount++;
                    }

                    dayCounter++;
                }

                Console.WriteLine("Successful entries count: " + EntriesCount);
                return timeTable;
            }
            catch (Exception parserException)
            {
                string date = DateTime.Now.ToString("HH:mm:ss");
                string time = DateTime.Now.ToShortDateString();

                //System.IO.File.Create($"error:{date}_{time}.log");
                //System.IO.File.WriteAllText($"error:{date}_{time}.log", parserException.ToString());
                Console.WriteLine("error in parser");

                //Dictionary<string, object> rt = new();
                return new();
            }
        }

        static async Task<HttpResponseMessage> GetRequest(string adress, Dictionary<string, string> Params)
        {
            HttpClient client = new HttpClient();

            try
            {
                Uri uri = new Uri(adress);
                var content = new FormUrlEncodedContent(Params);

                return await client.PostAsync(adress, content);
            }
            catch (Exception err)
            {
                Console.WriteLine("An unknown error occured: " + err.ToString());
            }
            finally
            {
                client.Dispose();
            }

            return null;
        }

        static async void sendSchedule(ITelegramBotClient bot, long chatId, CancellationToken token, int targetDay)
        {
        try
            {
                DateTime currentDate = DateTime.Now;
                int kostuL = (int)currentDate.DayOfWeek;
                int dayToParse = kostuL + targetDay;

                Console.WriteLine(dayToParse);
                switch (kostuL)
                {
                    case 0:
                        dayToParse = 7;
                        break;

                    case 8:
                        dayToParse = 0;
                        break;

                    default:
                        dayToParse--;
                        break;
                }

                string[] daysObject = { "Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота", "Воскресенье" };
                string BuildMessage = $"🗓️ {daysObject[dayToParse]}\n\n";

                Console.WriteLine(BuildMessage);
                bool markupContent = false;
                //dayToParse = Math.Clamp(dayToParse, 0, 6);
                Dictionary<string, object> timeTable = AngleSharpParse(chatId);
                List<List<InlineKeyboardButton>> buttons = new List<List<InlineKeyboardButton>>();

                Dictionary<string, object>? dayDict;
                if (timeTable.TryGetValue(dayToParse.ToString(), out var dayDictValue))
                {
                    dayDict = dayDictValue as Dictionary<string, object>;
                    if (dayDict != null)
                    {
                        foreach (var key in dayDict)
                        {
                            if (dayDict.TryGetValue(key.Key, out var lessonsDictValue))
                            {
                                var lessonsDict = lessonsDictValue as Dictionary<string, object>;
                                if (lessonsDict == null)
                                {
                                    await bot.SendTextMessageAsync(chatId, "❌ An error occured: NullReferenceException ХАХАХ Кстати BREAKPOINT: 1", cancellationToken: token);
                                    break;
                                }
                                {
                                    if (lessonsDict.TryGetValue("lessonsDict", out var lessonsDictValue2))
                                    {
                                        var lessonsDict2 = lessonsDictValue2 as Dictionary<string, object>;
                                        if (lessonsDict2 == null)
                                        {
                                            await bot.SendTextMessageAsync(chatId, "❌ An error occured: NullReferenceException ХАХАХ Кстати BREAKPOINT: 2", cancellationToken: token);
                                            break;
                                        }
                                        {
                                            string ord = lessonsDict2["ord"].ToString().Trim();
                                            string time = lessonsDict2["time"].ToString().Trim();
                                            string homeTask = lessonsDict2["hometask"].ToString().Trim().Replace("\n", "").Replace("  ", "");
                                            string mark = lessonsDict2["mark"].ToString().Trim();

                                            var reply = lessonsDict2["attach_name"].ToString().Split(",");
                                            var replyCtx = lessonsDict2["attach_content"].ToString().Split(",");

                                            for (int i = 0; i < reply.Length; i++)
                                            {
                                                if (replyCtx.Length <= i)
                                                {
                                                    continue;
                                                }

                                                string URI = replyCtx[i].ToString().Trim().Replace("\n", "");

                                                // УРА, ХОТЯ БЫ ВХАРДКОДИЛ!!!
                                                if (Uri.IsWellFormedUriString(URI, UriKind.Absolute))
                                                {
                                                    Uri url = new Uri(URI);

                                                    var button = new InlineKeyboardButton(reply[i])
                                                    {
                                                        Url = url.ToString()
                                                    };

                                                    int idx = homeTask.IndexOf(reply[i]);
                                                    if (idx != -1) homeTask = homeTask.Remove(idx > 0 ? idx - 1 : idx, reply[i].Length + 1);

                                                    buttons.Add(new List<InlineKeyboardButton> { button });
                                                }
                                                else
                                                {
                                                    Console.WriteLine("Got invalid uri, skipping..");
                                                }
                                            }

                                            if (reply.Length > 0) markupContent = true;

                                            BuildMessage = $"{BuildMessage}🎓 {ord}{lessonsDict2["name"].ToString()} [{time}]\n✍🏻 Д/З: {homeTask}\n📍 Оценка(и): {mark}\n\n";
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (markupContent != false)
                    {
                        //Console.WriteLine("rof");
                        await bot.SendTextMessageAsync(chatId, BuildMessage, replyMarkup: new InlineKeyboardMarkup(buttons), cancellationToken: token);

                    }
                    else
                    {
                        await bot.SendTextMessageAsync(chatId, BuildMessage, cancellationToken: token);
                    }
                }
            }

        catch
            {
                Console.WriteLine("An error occured in 'SendSchedule'");
            }
        }

        static async Task<HttpResponseMessage> GetHTTPRequest(string adress)
        {
            HttpClient client = new HttpClient();

            try
            {
                Uri uri = new Uri(adress);

                return await client.GetAsync(adress);
            }
            catch (Exception err)
            {
                Console.WriteLine("An unknown error occured: " + err.ToString());
            }
            finally
            {
                client.Dispose();
            }

            return null;
        }

        async static Task CallDefaultButtons(ITelegramBotClient bot, long chatId)
        {
            ReplyKeyboardMarkup replyKeyboardMarkup = new(new[]
            {
                    new KeyboardButton[] { "📅 Расписание на сегодня", "📅 Расписание на завтра"},
                    new KeyboardButton[] { "📅 Текущая неделя" }
                })
            {
                ResizeKeyboard = true
            };

            await bot.SendTextMessageAsync(chatId, "Hello there, choose)", replyMarkup: replyKeyboardMarkup);
        }

        public class Userdata
        {
            public string username { get; set; }
            public string password { get; set; }
            //public long chatId { get; set; }
        }

        async static Task Update(ITelegramBotClient bot, Update update, CancellationToken token)
        {
            //if (true) return;
            try
            {
                var ctx = update.Message;
                var msg = ctx.Text;
                var chatId = ctx.Chat.Id;

                Console.WriteLine("Handled new message in -> " + chatId.ToString());

                if (msg == null || ctx is null) return;

                //Console.WriteLine(ctx);

                switch (msg)
                {
                    default:
                        if (!msg.Contains("/auth", StringComparison.CurrentCulture))
                        {
                            await TG.SendTextMessageAsync(chatId, "⚠️ Your message couldn't be handled!\n\nℹ️ Make sure that request is correct aswell as you're authorized by /auth <username> <password>", cancellationToken: token);

                            break;
                        };

                        string[] auth_data = msg.Replace("/auth ", "").Split();

                        byte[] key_cipher = new byte[32];
                        Array.Fill(key_cipher, (byte)0);

                        byte[] iv = new byte[16];
                        Array.Fill(iv, (byte)0);

                        byte[] encrypted_login = Encrypt(auth_data[0], key_cipher, iv);
                        byte[] encrypted_pass = Encrypt(auth_data[1], key_cipher, iv);

                        var userdata = new Userdata
                        {
                            username = Convert.ToBase64String(encrypted_login),
                            password = Convert.ToBase64String(encrypted_pass)
                        };

                        string json = JsonConvert.SerializeObject(userdata);
                        System.IO.File.WriteAllText($"db/{chatId}.txt", json);

                        await TG.SendTextMessageAsync(chatId, $"💫 I added you to the database, if smth didn't work, check if the username and password are correct\n\nLinked to: {auth_data[0]}", cancellationToken: token);
                        break;
                    case "/start":
                        await CallDefaultButtons(bot, chatId);
                        break;

                    case "📅 Расписание на завтра":
                        sendSchedule(bot, chatId, token, 1);
                        break;

                    case "📅 Расписание на сегодня":
                        sendSchedule(bot, chatId, token, 0);
                        break;

                    case "📅 Текущая неделя":
                        Dictionary<string, object> timeTableQ = AngleSharpParse(chatId);

                        for (int dayIter = 0; dayIter < timeTableQ.Count; dayIter++)
                        {
                            string[] daysObjectQ = { "Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота", "Воскресенье" };
                            string BuildMessageQ = $"🗓️ {daysObjectQ[dayIter]}\n\n\n";

                            bool markupContentQ = false;
                            List<List<InlineKeyboardButton>> buttonsQ = new List<List<InlineKeyboardButton>>();

                            Dictionary<string, object>? dayDictQ;
                            if (timeTableQ.TryGetValue(dayIter.ToString(), out var dayDictValueQ))
                            {
                                dayDictQ = dayDictValueQ as Dictionary<string, object>;
                                if (dayDictQ != null)
                                {
                                    foreach (var keyQ in dayDictQ)
                                    {
                                        if (dayDictQ.TryGetValue(keyQ.Key, out var lessonsDictValueQ))
                                        {
                                            var lessonsDictQ = lessonsDictValueQ as Dictionary<string, object>;
                                            if (lessonsDictQ == null)
                                            {
                                                await bot.SendTextMessageAsync(chatId, "❌ An error occured: NullReferenceException ХАХАХ Кстати BREAKPOINT: 1");
                                                break;
                                            }
                                            {
                                                if (lessonsDictQ.TryGetValue("lessonsDict", out var lessonsDictValue2Q))
                                                {
                                                    var lessonsDict2Q = lessonsDictValue2Q as Dictionary<string, object>;
                                                    if (lessonsDict2Q == null)
                                                    {
                                                        await bot.SendTextMessageAsync(chatId, "❌ An error occured: NullReferenceException ХАХАХ Кстати BREAKPOINT: 2");
                                                        break;
                                                    }
                                                    {
                                                        string ord = lessonsDict2Q["ord"].ToString().Trim();
                                                        string time = lessonsDict2Q["time"].ToString().Trim();
                                                        string homeTask = lessonsDict2Q["hometask"].ToString().Trim().Replace("\n", "");
                                                        string mark = lessonsDict2Q["mark"].ToString().Trim();

                                                        var replyQ = lessonsDict2Q["attach_name"].ToString().Split(",");
                                                        var replyCtxQ = lessonsDict2Q["attach_content"].ToString().Split(",");

                                                        for (int i = 0; i < replyCtxQ.Length; i++)
                                                        {
                                                            if (replyCtxQ.Length <= i)
                                                            {
                                                                continue;
                                                            }

                                                            string URI = replyCtxQ[i].ToString().Trim().Replace("\n", "");

                                                            if (Uri.IsWellFormedUriString(URI, UriKind.Absolute))
                                                            {
                                                                Uri url = new Uri(URI);

                                                                var button = new InlineKeyboardButton(replyQ[i])
                                                                {
                                                                    Url = url.ToString()
                                                                };

                                                                int idx = homeTask.IndexOf(replyQ[i]);
                                                                if (idx != -1) homeTask = homeTask.Remove(idx > 0 ? idx - 1 : idx, replyQ[i].Length + 1);

                                                                buttonsQ.Add(new List<InlineKeyboardButton> { button });
                                                            }
                                                        }

                                                        if (replyQ.Length > 0) markupContentQ = true;

                                                        BuildMessageQ = $"{BuildMessageQ}🎓 {ord}{lessonsDict2Q["name"].ToString()} [{time}]\n✍🏻 Д/З: {homeTask}\n📍 Оценка(и): {mark}\n\n";
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }

                                if (markupContentQ != false)
                                {
                                    await bot.SendTextMessageAsync(chatId, BuildMessageQ, replyMarkup: new InlineKeyboardMarkup(buttonsQ), cancellationToken: token);
                                }
                                else
                                {
                                    await bot.SendTextMessageAsync(chatId, BuildMessageQ, cancellationToken: token);
                                }
                            }
                        }
                        break;

                }
            }

            catch
            {
                Console.WriteLine("an error occured in telegram.bot");
            }
        }

        private static Task Error(ITelegramBotClient arg1, Exception telegramException, CancellationToken arg3)
        {
            //Console.WriteLine(telegramException);
            TG.StartReceiving(Update, Error, cancellationToken: arg3);

            Console.WriteLine(telegramException);
            throw telegramException;
        }
    }
}
