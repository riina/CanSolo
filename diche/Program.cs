using System.Globalization;
using System.Text.Json;
using Discord;
using Discord.WebSocket;
using QuickSetup;

const string BaseUrl = "https://diverse.direct/wp-json/dd-front/v1/items?page={0}";
const string EnvToken = "diche_discord_token";
const string EnvChannel = "diche_discord_channel";

int skip = args.Length == 0 ? 0 : int.Parse(args[0]);
(DiscordSocketClient discord, IMessageChannel textChan) = await QuickDiscord.CreateAsync(EnvToken, EnvChannel);
using DiscordSocketClient d = discord;
const int delaySec = 60;
HttpClient http = new();
Console.Write("Initial fetch... ");
HashSet<long> ids = new((await GetPage(http, 1)).Skip(skip).Select(v => v.id));
Console.WriteLine($"{ids.Count} (skip {skip})");
while (true)
{
    await Task.Delay(TimeSpan.FromSeconds(delaySec));
    List<Item> newProducts = new();
    List<Item> retrieved = new();
    try
    {
        int page = 1;
        do
        {
            Console.Write($"Fetch {DateTimeOffset.Now} (page {page})... ");
            retrieved.Clear();
            retrieved.AddRange(await GetPage(http, page++));
            newProducts.AddRange(retrieved.Where(v => !ids.Contains(v.id)));
        } while (retrieved.Count != 0 && !retrieved.Select(v => v.id).Intersect(ids).Any());
        Console.WriteLine($"{newProducts.Count}");
        foreach (Item? product in newProducts)
        {
            try
            {
                await Send(textChan, product);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            ids.Add(product.id);
        }
        newProducts.Clear();
    }
    catch (Exception e)
    {
        Console.WriteLine(e);
    }
}

static async Task Send(IMessageChannel chan, Item info)
    => await chan.SendMessageAsync(embed: new EmbedBuilder()
    {
        ImageUrl = info.thumbnail,
        Author = new EmbedAuthorBuilder { Name = info.circle },
        Title = info.title,
        Url = info.url,
        Fields = new()
        {
            new EmbedFieldBuilder().WithName("Price").WithValue(string.IsNullOrWhiteSpace(info.salePrice)
                ? info.price
                : $"{info.price} => {info.salePrice}")
        }
    }.Build());

static async Task<IEnumerable<Item>> GetPage(HttpClient client, int page)
    => (await JsonSerializer.DeserializeAsync<ItemsResponse>(
        await client.GetStreamAsync(string.Format(CultureInfo.InvariantCulture, BaseUrl, page))))!.data.ToList();

internal record ItemsResponse(List<Item> data);

internal record Item(long id, string title, string url, string thumbnail, string circle, string price, string salePrice, bool isSale, bool newly);
