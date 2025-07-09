using Microsoft.AspNetCore.Mvc;
using Radzen.Blazor;

//using Microsoft.EntityFrameworkCore;
//using TimeZoneConverter;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;


[Route("api/[controller]")]
[ApiController]
public class Photo(IHttpClientFactory _httpClientFactory) : ControllerBase
{
    [HttpPost("SaveData")]
    public async Task<IActionResult> SaveData([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");
        // Validate file type (optional)
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(fileExtension))
            return BadRequest("Invalid file type. Only image files are allowed.");

        var mimeType = fileExtension switch
        {
            ".jpg" or ".jpeg" => "jpeg",
            ".png" => "png",
            ".gif" => "gif",
            ".webp" => "webp",
            _ => "jpeg" // default
        };


        try
        {
            // Read the image file
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            var imageBytes = memoryStream.ToArray();
            var base64Image = Convert.ToBase64String(imageBytes);

            var imageUrl = $"data:image/{mimeType};base64,{base64Image}";
            var prompt = "Analyze the uploaded file(s) and prepare a detailed fit-out work estimation based on Dubai market prices (2025).Exclude loose furniture. Include only fixed elements like joinery, walls, ceilings, lights, floors, HVAC, etc.Note:If multiple images show the same space from different angles, calculate quantities once (do not duplicate measurements).Return a response as strictly valid JSON without any invisible characters or formatting issues object with two fields:items:An array of objects where each contains category, description, quantity,unit, unitRate, and totalCost. grandTotal: A separate numeric value representing the sum of all totalCost values.Fit-Out Scope to Include:    1. Joinery Works ▪ Built-in wardrobes, cabinets, counters, wall cladding, shelving, panels    2. Wall Finishes ▪ Painting, wallpaper, acoustic/décor panels, Tile    3. Flooring ▪ Tiles, wood, carpet, skirting    4. Ceiling Works ▪ Gypsum ceilings, bulkheads, coffers, cornices    5. Lighting Fixtures ▪ Downlights, LED strips, pendants, cove lights    6. Electrical Fixtures ▪ Switches, sockets, DB boxes, visible conduits    7. HVAC Elements ▪ AC supply/return grills, diffusers, ducts    8. Doors & Partitions ▪ Timber/glass doors, frames, handles, locks    9. Plumbing & Sanitary ▪ Washbasins, vanities, exposed plumbing, accessories    10. Glass & Aluminum ▪ Glass partitions, cubicles, aluminum profiles    11. Fire & Safety Fixtures ▪ Smoke detectors, sprinklers, exit signs    12. Signage & Branding (if visible) ▪ Internal signs, vinyl graphics    13. IT & Data Points ▪ Floor boxes, data outlets (visible only)For Each Item, Provide:    • Description (e.g., “Gypsum ceiling with cove lighting”)    • Estimated Quantity (sqm, rmt, or units as applicable)    • Unit Rate in AED (based on 2025 Dubai market, mid-range finish)    • Total Cost (AED)";

            // Prepare the request to OpenAI
            var openAiApiKey = "****";
            if (string.IsNullOrEmpty(openAiApiKey))
                return StatusCode(500, "OpenAI API key is not configured.");

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", openAiApiKey);

           var requestBody = new
{
    model = "gpt-4o", // or another vision model
    messages = new object[]
    {
        new
        {
            role = "system",
            content = "You are a professional interior designer and cost estimator."
        },
        new
        {
            role = "user",
            content = new object[]
            {
                new { type = "text", text = prompt },
                new
                {
                    type = "image_url",
                    image_url = new
                    { 
                        url = imageUrl,
                        //detail = "low"  // Added image processing detail level
                    }
                }
            }
        }
    },
    max_tokens = 2000
};
            var response = await client.PostAsync(
                "https://api.openai.com/v1/chat/completions",
                new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"));

            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Full API Response: {responseContent}"); // Log this



            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, new
                {
                    Error = "OpenAI API request failed",
                    StatusCode = response.StatusCode,
                    Content = responseContent
                });
            }


            var result = JsonSerializer.Deserialize<OpenAiResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var choice = (from data in result.Choices
                          select data).First();
            var res = choice.Message.Content;
            string cleanJson = res
            .Replace("```json", "")
            .Replace("```", "")
            .Trim();
            Console.WriteLine($"Full clean deserialize Response: {cleanJson}");
            var MainResponse=JsonSerializer.Deserialize<ServerResponse>(cleanJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
          
            
            return Ok(MainResponse);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }



    }
    // Helper classes for OpenAI response deserialization


}
public class Item
{
    public string Category { get; set; }
    public string Description { get; set; }
    public decimal Quantity { get; set; }  
    public string Unit { get; set; }
    public decimal UnitRate { get; set; }
    public decimal TotalCost { get; set; }
}

public class ServerResponse
{
    public List<Item> Items { get; set; }
    public decimal GrandTotal { get; set; }
}

//  your response model classes
public class OpenAiResponse
{
    public string Id { get; set; }
    public string Object { get; set; }
    public long Created { get; set; }
    public string Model { get; set; }
    public List<OpenAiChoice> Choices { get; set; }
    public OpenAiUsage Usage { get; set; }
}

public class OpenAiChoice
{
    public int Index { get; set; }
    public OpenAiMessage Message { get; set; }
    public object Logprobs { get; set; }
    public string FinishReason { get; set; }
}

public class OpenAiMessage
{
    public string Role { get; set; }
    public string Content { get; set; }
}

public class OpenAiUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}
 