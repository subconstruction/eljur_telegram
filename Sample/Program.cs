using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Xml.Linq;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.InlineQueryResults;

namespace Test
{
    class Program
    {
        private static readonly string TelegramToken = ""; // Insert your telegram bot token here
        private static readonly System.Timers.Timer Timer = new System.Timers.Timer();
        private static readonly TelegramBotClient BotClient = new TelegramBotClient(TelegramToken);

        static async Task Main(string[] args)
        {
            try
            {
                InitializeDirectories();
                Console.Title = "Actual Information Updater";

                BotClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync);

                Timer.Interval = 86000;
                Timer.Elapsed += TimerElapsedAsync;
                Timer.Start();

                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in main: {ex}");
            }
        }

        private static void InitializeDirectories()
        {
            CreateDirectoryIfNotExists("db");
            CreateDirectoryIfNotExists("db/cache");
        }

        private static void CreateDirectoryIfNotExists(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    Console.WriteLine($"Folder created successfully at {path}");
                }
                else
                {
                    Console.WriteLine($"Folder already exists at {path}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"An error occurred while creating directory {path}: {e.Message}");
            }
        }

        private static async void TimerElapsedAsync(object sender, ElapsedEventArgs e)
        {
            foreach (string file in Directory.EnumerateFiles("db/", "*.txt"))
            {
                await Task.Delay(1000);
                long chatId = Convert.ToInt64(Path.GetFileNameWithoutExtension(file));
                string cachePath = $"db/cache/{chatId}.txt";
                bool cacheExists = System.IO.File.Exists(cachePath);

                var timeTable = ParseTimeTable(chatId);
                string encryptedData = EncryptData(JsonConvert.SerializeObject(timeTable));

                if (!cacheExists)
                {
                    System.IO.File.WriteAllText(cachePath, encryptedData);
                    continue;
                }

                var lastContent = JsonConvert.DeserializeObject<Dictionary<string, object>>(DecryptData(System.IO.File.ReadAllText(cachePath)));
                await CompareAndNotifyChangesAsync(chatId, timeTable, lastContent);

                System.IO.File.WriteAllText(cachePath, encryptedData);
            }
        }

        private static async Task CompareAndNotifyChangesAsync(long chatId, Dictionary<string, object> timeTable, Dictionary<string, object> lastContent)
        {
            string[] daysOfWeek = { "Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота", "Воскресенье" };

            for (int dayIndex = 0; dayIndex < timeTable.Count; dayIndex++)
            {
                if (!timeTable.TryGetValue(dayIndex.ToString(), out var currentDay) || !(currentDay is Dictionary<string, object> currentDayDict))
                    continue;

                if (!lastContent.TryGetValue(dayIndex.ToString(), out var lastDay) || !(lastDay is Dictionary<string, object> lastDayDict))
                    continue;

                bool isChanged = false;
                string message = $"⚠️ Обновлен день [{daysOfWeek[dayIndex]}]\n\n\n";

                foreach (var key in currentDayDict.Keys)
                {
                    if (!currentDayDict.TryGetValue(key, out var currentLessonObj) || !(currentLessonObj is Dictionary<string, object> currentLesson))
                        continue;

                    if (!lastDayDict.TryGetValue(key, out var lastLessonObj) || !(lastLessonObj is Dictionary<string, object> lastLesson))
                        continue;

                    string currentName = currentLesson["name"].ToString().Trim();
                    string currentHomeTask = currentLesson["hometask"].ToString().Trim().Replace("\n", "");
                    string currentMark = currentLesson["mark"].ToString().Trim().Replace(" ", "").Replace("×1", "");

                    string lastHomeTask = lastLesson["hometask"].ToString().Trim().Replace("\n", "");
                    string lastMark = lastLesson["mark"].ToString().Trim().Replace(" ", "").Replace("×1", "");

                    if (currentHomeTask != lastHomeTask)
                    {
                        await BotClient.SendTextMessageAsync(chatId, $"💬 {currentName}: Заданно Д/З -> {currentHomeTask}");
                    }

                    if (currentMark != lastMark)
                    {
                        await BotClient.SendTextMessageAsync(chatId, $"💬 {currentName}: Новая Оценка -> {currentMark.Replace(lastMark, "").Replace("/", "")}");
                    }

                    isChanged |= currentName != lastLesson["name"].ToString().Trim();
                    message += $"🎓 {currentLesson["ord"]}{currentLesson["name"]} [{currentLesson["time"]}] " +
                               $"\n✍🏻 Д/З: {currentHomeTask}\n📍 Оценка(и): {currentMark}\n\n";
                }

                if (isChanged)
                {
                    await BotClient.SendTextMessageAsync(chatId, message);
                }
            }
        }

        private static string EncryptData(string plainText)
        {
            byte[] key = new byte[32];
            byte[] iv = new byte[16];
            Array.Fill(key, (byte)0);
            Array.Fill(iv, (byte)0);

            using Aes aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;

            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using MemoryStream msEncrypt = new();
            using (CryptoStream csEncrypt = new(msEncrypt, encryptor, CryptoStreamMode.Write))
            using (StreamWriter swEncrypt = new(csEncrypt))
            {
                swEncrypt.Write(plainText);
            }

            return Convert.ToBase64String(msEncrypt.ToArray());
        }

        private static string DecryptData(string cipherText)
        {
            byte[] key = new byte[32];
            byte[] iv = new byte[16];
            Array.Fill(key, (byte)0);
            Array.Fill(iv, (byte)0);

            using Aes aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;

            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using MemoryStream msDecrypt = new(Convert.FromBase64String(cipherText));
            using (CryptoStream csDecrypt = new(msDecrypt, decryptor, CryptoStreamMode.Read))
            using (StreamReader srDecrypt = new(csDecrypt))
            {
                return srDecrypt.ReadToEnd();
            }
        }

        private static Dictionary<string, object> ParseTimeTable(long chatId)
        {
            try
            {
                string filePath = $"db/{chatId}.txt";
                if (!System.IO.File.Exists(filePath))
                {
                    BotClient.SendTextMessageAsync(chatId, "👤 User not detected! Authorize using the following syntax:\n\n/auth <username> <password>");
                    return new();
                }

                var parser = new HtmlParser();
                var userData = JsonConvert.DeserializeObject<UserData>(System.IO.File.ReadAllText(filePath));

                string journalBody = GetRequest("https://kip.eljur.ru/?show=home", userData).Result;
                var document = parser.ParseDocument(journalBody);

                return ExtractTimeTable(document);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in parser: {ex}");
                return new();
            }
        }

        private static Dictionary<string, object> ExtractTimeTable(IDocument document)
        {
            var timeTable = new Dictionary<string, object>();
            int dayCounter = 0;

            foreach (IElement dayElement in document.GetElementsByClassName("dnevnik-day"))
            {
                var lessons = ExtractLessons(dayElement);
                timeTable.Add(dayCounter.ToString(), lessons);
                dayCounter++;
            }

            return timeTable;
        }

        private static Dictionary<string, object> ExtractLessons(IElement dayElement)
        {
            var lessonsDict = new Dictionary<string, object>();
            int lessonCount = 0;

            foreach (IElement lessonElement in dayElement.GetElementsByClassName("dnevnik-lesson"))
            {
                var lesson = ParseLesson(lessonElement);
                lessonsDict.Add(lessonCount.ToString(), lesson);
                lessonCount++;
            }

            return lessonsDict;
        }

        private static Dictionary<string, object> ParseLesson(IElement lessonElement)
        {
            string lessonNum = lessonElement.QuerySelector(".dnevnik-lesson__number").TextContent.Trim();
            string lessonName = lessonElement.QuerySelector(".js-rt_licey-dnevnik-subject").TextContent.Trim();
            string lessonTime = lessonElement.QuerySelector(".dnevnik-lesson__time").TextContent.Trim();

            string lessonMark = ExtractMarks(lessonElement);
            string lessonHomeTask = ExtractHomeTask(lessonElement, out string attachName, out string attachContent);

            return new Dictionary<string, object>
            {
                { "ord", lessonNum },
                { "name", lessonName },
                { "time", lessonTime },
                { "hometask", lessonHomeTask },
                { "mark", lessonMark },
                { "attach_name", attachName },
                { "attach_content", attachContent }
            };
        }

        private static string ExtractMarks(IElement lessonElement)
        {
            var marks = lessonElement.QuerySelectorAll(".dnevnik-mark");

            if (marks.Length == 0)
            {
                return "Нет";
            }

            var marksList = new List<string>();

            foreach (var mark in marks)
            {
                marksList.Add(mark.TextContent.Trim());
            }

            return string.Join(" | ", marksList);
        }

        private static string ExtractHomeTask(IElement lessonElement, out string attachName, out string attachContent)
        {
            attachName = string.Empty;
            attachContent = string.Empty;
            string homeTask = "Нет";

            var taskElement = lessonElement.QuerySelector(".dnevnik-lesson__task");
            if (taskElement != null)
            {
                homeTask = taskElement.TextContent.Trim();
                foreach (var link in taskElement.QuerySelectorAll(".b-href.external-link"))
                {
                    attachName += $",{link.TextContent}";
                    attachContent += $",{link.GetAttribute("href")}";
                }
            }

            return homeTask;
        }

        private static async Task<string> GetRequest(string url, UserData userData)
        {
            using HttpClient client = new();
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "username", DecryptData(userData.Username) },
                { "password", DecryptData(userData.Password) }
            });

            var response = await client.PostAsync(url, content);
            return await response.Content.ReadAsStringAsync();
        }

        private static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken token)
        {
            try
            {
                var message = update.Message;
                if (message?.Text == null) return;

                var chatId = message.Chat.Id;
                Console.WriteLine($"Handled new message from {chatId}");

                DateTime today = DateTime.Now;
                int todayDayIndex = (int)today.DayOfWeek - 1;
                if (todayDayIndex < 0) todayDayIndex = 6;

                DateTime tomorrow = today.AddDays(1);
                int tomorrowDayIndex = (int)tomorrow.DayOfWeek - 1;
                if (tomorrowDayIndex < 0) tomorrowDayIndex = 6;

                switch (message.Text)
                {
                    case "/start":
                        await SendDefaultButtonsAsync(bot, chatId);
                        break;
                    case "/auth":
                        await HandleAuthenticationAsync(bot, message, token);
                        break;
                    case "📅 Расписание на завтра":
                        await SendScheduleAsync(bot, chatId, token, tomorrowDayIndex);
                        break;
                    case "📅 Расписание на сегодня":
                        await SendScheduleAsync(bot, chatId, token, todayDayIndex);
                        break;
                    case "📅 Текущая неделя":
                        await SendWeekScheduleAsync(bot, chatId, token);
                        break;
                    default:
                        if (message.Text.Contains("/auth", StringComparison.CurrentCulture))
                        {
                            await HandleAuthenticationAsync(bot, message, token);
                        }
                        await bot.SendTextMessageAsync(chatId, "⚠️ Your message couldn't be handled! Ensure the request is correct or you're authorized with /auth <username> <password>", cancellationToken: token);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while handling update: {ex}");
            }
        }

        private static async Task HandleAuthenticationAsync(ITelegramBotClient bot, Message message, CancellationToken token)
        {
            var chatId = message.Chat.Id;
            var authData = message.Text.Replace("/auth ", "").Split();

            if (authData.Length != 2)
            {
                await bot.SendTextMessageAsync(chatId, "❌ Invalid authentication format. Use /auth <username> <password>", cancellationToken: token);
                return;
            }

            byte[] key = new byte[32];
            byte[] iv = new byte[16];
            Array.Fill(key, (byte)0);
            Array.Fill(iv, (byte)0);

            var userData = new UserData
            {
                Username = EncryptData(authData[0]),
                Password = EncryptData(authData[1])
            };

            string json = JsonConvert.SerializeObject(userData);
            System.IO.File.WriteAllText($"db/{chatId}.txt", json);

            await bot.SendTextMessageAsync(chatId, $"💫 You have been added to the database. If something didn't work, check your username and password.\n\nLinked to: {authData[0]}", cancellationToken: token);
        }

        private static async Task SendScheduleAsync(ITelegramBotClient bot, long chatId, CancellationToken token, int targetDay)
        {
            try
            {
                var timeTable = ParseTimeTable(chatId);
                string message = BuildScheduleMessage(timeTable, targetDay, out var buttons);

                if (message is null) return;

                if (buttons.Any())
                {
                    await bot.SendTextMessageAsync(chatId, message, replyMarkup: new InlineKeyboardMarkup(buttons), cancellationToken: token);
                }
                else
                {
                    await bot.SendTextMessageAsync(chatId, message, cancellationToken: token);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while sending schedule: {ex}");
            }
        }

        private static async Task SendWeekScheduleAsync(ITelegramBotClient bot, long chatId, CancellationToken token)
        {
            var timeTable = ParseTimeTable(chatId);
            for (int dayIndex = 0; dayIndex < timeTable.Count; dayIndex++)
            {
                string message = BuildScheduleMessage(timeTable, dayIndex, out var buttons);
                if (message is null) continue;

                if (buttons.Any())
                {
                    await bot.SendTextMessageAsync(chatId, message, replyMarkup: new InlineKeyboardMarkup(buttons), cancellationToken: token);
                }
                else
                {
                    await bot.SendTextMessageAsync(chatId, message, cancellationToken: token);
                }
            }
        }

        private static string BuildScheduleMessage(Dictionary<string, object> timeTable, int dayIndex, out List<List<InlineKeyboardButton>> buttons)
        {
            buttons = new List<List<InlineKeyboardButton>>();
            string[] daysOfWeek = { "Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота", "Воскресенье" };

            if (!timeTable.TryGetValue(dayIndex.ToString(), out var day) || !(day is Dictionary<string, object> dayDict))
                return string.Empty;

            string message = $"🗓️ {daysOfWeek[dayIndex]}\n\n";
            bool isDayAvaliable = dayDict.Keys.Count > 0;

            if (!isDayAvaliable) return null;

            foreach (var key in dayDict.Keys)
            {
                if (!dayDict.TryGetValue(key, out var lessonObj) || !(lessonObj is Dictionary<string, object> lessonDict))
                    continue;

                string ord = lessonDict.ContainsKey("ord") ? lessonDict["ord"].ToString() : "N/A";

                string homeTask = lessonDict["hometask"].ToString().Trim();
                string[] attachNames = lessonDict["attach_name"].ToString().Split(',');
                string[] attachContents = lessonDict["attach_content"].ToString().Split(',');

                for (int i = 0; i < attachNames.Length; i++)
                {
                    if (i >= attachContents.Length || !Uri.IsWellFormedUriString(attachContents[i], UriKind.Absolute))
                        continue;

                    var button = new InlineKeyboardButton(attachNames[i])
                    {
                        Url = attachContents[i]
                    };

                    buttons.Add(new List<InlineKeyboardButton> { button });

                    int idx = homeTask.IndexOf(attachNames[i], StringComparison.Ordinal);
                    if (idx != -1)
                    {
                        homeTask = homeTask.Remove(idx > 0 ? idx - 1 : idx, attachNames[i].Length + 1);
                    }
                }

                message += $"🎓 {ord} {lessonDict["name"]} [{lessonDict["time"]}] " +
                           $"\n✍🏻 Д/З: {homeTask}\n📍 Оценка(и): {lessonDict["mark"]}\n\n";
            }

            return message;
        }

        private static async Task SendDefaultButtonsAsync(ITelegramBotClient bot, long chatId)
        {
            var replyKeyboardMarkup = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "📅 Расписание на сегодня", "📅 Расписание на завтра" },
                new KeyboardButton[] { "📅 Текущая неделя" }
            })
            {
                ResizeKeyboard = true
            };

            await bot.SendTextMessageAsync(chatId, "Hello there, choose)", replyMarkup: replyKeyboardMarkup);
        }

        private static Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, CancellationToken token)
        {
            Console.WriteLine($"Telegram bot error: {ex}");
            return Task.CompletedTask;
        }

        private class UserData
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }
    }
}
