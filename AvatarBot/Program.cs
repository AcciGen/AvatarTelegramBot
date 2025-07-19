using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace AvatarBot
{
    internal class Program
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private const string _telegramBotToken = "7697041054:AAF9MI4MK_x8EgFK2etkX3jUZ-VW-Eq3HX0";

        private static readonly Dictionary<string, string> _dicebearCommands = new()
        {
            { "/fun-emoji", "fun-emoji" },
            { "/bottts", "bottts" },
            { "/avataaars", "avataaars" },
            { "/pixel-art", "pixel-art" }
        };

        private static async Task Main(string[] args)
        {
            var telegramBotClient = new TelegramBotClient(_telegramBotToken);

            ReceiverOptions receiverOptions = new() { AllowedUpdates = Array.Empty<UpdateType>() };
            using CancellationTokenSource cancellationTokenSource = new();

            telegramBotClient.StartReceiving(updateHandler: HandleUpdateAsync,
                                             errorHandler: HandleErrorAsync,
                                             receiverOptions: receiverOptions,
                                             cancellationToken: cancellationTokenSource.Token);

            Console.WriteLine("Bot is running. Press Enter to exit.");
            Console.ReadLine();
            cancellationTokenSource.Cancel();

            async Task HandleUpdateAsync(ITelegramBotClient telegramBotClient, Update update, CancellationToken cancellationToken)
            {
                var updateHandler = update.Type switch
                {
                    UpdateType.Message => HandleMessageAsync(telegramBotClient, update, cancellationToken),
                    _ => Task.CompletedTask
                };

                try
                {
                    await updateHandler;
                }
                catch (Exception exception)
                {
                    throw new Exception(exception.Message);
                }
            }

            Task HandleErrorAsync(ITelegramBotClient telegramBotClient, Exception exception, CancellationToken cancellationToken)
            {
                var ErrorMessage = exception switch
                {
                    ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                    _ => exception.ToString()
                };

                throw new Exception(ErrorMessage);
            }
        }

        private static async Task HandleMessageAsync(ITelegramBotClient telegramBotClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Message is not { } message || message.Text is not { } messageText)
                return;

            var chatId = message.Chat.Id;

            var firstSpace = messageText.IndexOf(' ');
            var command = firstSpace == -1 ? messageText : messageText[..firstSpace];
            var seed = firstSpace == -1 ? null : messageText[(firstSpace + 1)..].Trim();

            await ProcessCommmandAsync(telegramBotClient, chatId, command, seed, cancellationToken);
        }

        private static async Task ProcessCommmandAsync(ITelegramBotClient telegramBotClient, long chatId, string? command, string? seed, CancellationToken cancellationToken)
        {
            if (_dicebearCommands.TryGetValue(command, out var dicebearCommand))
            {
                if (string.IsNullOrWhiteSpace(seed))
                {
                    await telegramBotClient.SendMessage(chatId: chatId,
                                                        text: "Iltimos, buyruqdan keyin matn (seed) kiriting. Misol: /fun-emoji Ali",
                                                        cancellationToken: cancellationToken);

                    LogToConsole(chatId, command, EStatus.None.ToString(), EStatus.SeedNotProvided.ToString());
                    return;
                }

                var url = $"https://api.dicebear.com/8.x/{dicebearCommand}/png?seed={Uri.EscapeDataString(seed)}";
                try
                {
                    using var response = await _httpClient.GetAsync(url, cancellationToken);
                    var statusCode = (int)response.StatusCode;

                    if (!response.IsSuccessStatusCode)
                    {
                        await telegramBotClient.SendMessage(chatId: chatId,
                                                            text: "Avatar yaratishda xatolik yuz berdi. Keyinroq urinib ko'ring.",
                                                            cancellationToken: cancellationToken);

                        LogToConsole(chatId, command, seed, $"{statusCode} - {EStatus.DicebearError.ToString()}");
                        return;
                    }

                    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    try
                    {
                        await telegramBotClient.SendPhoto(chatId: chatId,
                                                          photo: InputFile.FromStream(stream, fileName: $"{seed}.png"),
                                                          cancellationToken: cancellationToken);

                        LogToConsole(chatId, command, seed, $"{statusCode} - {EStatus.Success.ToString()}");
                    }
                    catch (Exception exception)
                    {
                        await telegramBotClient.SendMessage(chatId: chatId,
                                                            text: "Rasmni yuborishda xatolik yuz berdi.",
                                                            cancellationToken: cancellationToken);

                        LogToConsole(chatId, command, seed, $"{statusCode} - {exception.Message}");
                    }
                }
                catch (Exception exception)
                {
                    await telegramBotClient.SendMessage(chatId: chatId,
                                                        text: "Avatar yaratishda xatolik yuz berdi. Keyinroq urinib ko'ring.",
                                                        cancellationToken: cancellationToken);

                    LogToConsole(chatId, command, seed, $"{exception.Message}");
                }
            }
            else if (command.StartsWith("/"))
            {
                if (command == "/start")
                {
                    await telegramBotClient.SendMessage(chatId: chatId,
                                                        text: "👋 Assalomu alaykum! Avatar yaratish botiga xush kelibsiz!\n\n" +
                                                              "Bu bot yordamida siz turli uslublarda avatar rasmlar yaratishingiz mumkin.\n\n" +
                                                              "Yordam uchun /help buyrug'ini yuboring.",
                                                        parseMode: ParseMode.Markdown,
                                                        cancellationToken: cancellationToken);
                }
                else if (command == "/help")
                {
                    await telegramBotClient.SendMessage(chatId: chatId,
                                                        text: "🧠 *Botdan foydalanish yo'riqnomasi:*\n\n" +
                                                              "Quyidagi buyruqlardan birini yuboring va orqasidan tasodifiy so'z (seed) yozing:\n" +
                                                              "🔹 `/fun-emoji` – Emoji asosida kulgili avatar\n" +
                                                              "🔹 `/bottts` – Robot uslubidagi avatar\n" +
                                                              "🔹 `/avataaars` – Oddiy insoniy avatarlar\n" +
                                                              "🔹 `/pixel-art` – Pixel uslubidagi avatar\n\n" +
                                                              "📌 Masalan: `/bottts John`\n" +
                                                              "Bot sizga avatar rasmni qaytaradi.\n\n" +
                                                              "❓ Soz kiritilmasa, bot xatolik haqida ogohlantiradi.\n\n",
                                                        parseMode: ParseMode.Markdown,
                                                        cancellationToken: cancellationToken);
                }
                else
                {
                    await telegramBotClient.SendMessage(chatId: chatId,
                                                        text: "Noma'lum buyruq. Quyidagilardan birini ishlating: `/fun-emoji`, `/bottts`, `/avataaars`, `/pixel-art`, `/help`",
                                                        cancellationToken: cancellationToken);

                    LogToConsole(chatId, command, EStatus.None.ToString(), $"{EStatus.UnknownCommand.ToString()}");
                }
            }
            else
            {
                await telegramBotClient.SendMessage(chatId: chatId,
                                                    text: "Iltimos, avatar olish uchun buyruqdan foydalaning.",
                                                    cancellationToken: cancellationToken);

                LogToConsole(chatId, command, EStatus.None.ToString(), "command not provided");
            }
        }

        private static void LogToConsole(long chatId, string command, string seed, string status)
        {
            Console.WriteLine($"UserId: {chatId} || Command: {command} || Seed: {seed} || Status: {status}");
        }
    }
}
