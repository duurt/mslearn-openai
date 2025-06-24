using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using OpenAI.Images;

namespace dalle_client_v2
{
    class Program
    {
        private static readonly HttpClient httpClient = new HttpClient();

        static async Task Main(string[] args)
        {
            Console.Clear();
            Console.WriteLine("DALL-E Image Generator - Azure OpenAI 2.0");
            Console.WriteLine("==========================================");

            try
            {
                // Load config from appsettings.json
                IConfiguration config = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json")
                    .Build();

                string? endpoint = config["OPENAI_ENDPOINT"];
                string? apiKey = config["OPENAI_API_KEY"];
                string? modelDeployment = config["MODEL_DEPLOYMENT"];

                // Validate configuration
                if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(modelDeployment))
                {
                    Console.WriteLine("Error: Please check your appsettings.json configuration.");
                    Console.WriteLine("Required: OPENAI_ENDPOINT, OPENAI_API_KEY, MODEL_DEPLOYMENT");
                    return;
                }

                // Initialize Azure OpenAI client (2.0 syntax)
                var azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
                var imageClient = azureClient.GetImageClient(modelDeployment);

                int imageCount = 0;
                string? inputText = "";

                Console.WriteLine($"Connected to: {endpoint}");
                Console.WriteLine($"Using deployment: {modelDeployment}");
                Console.WriteLine();

                while (inputText?.ToLower() != "quit")
                {
                    Console.WriteLine("Enter your image prompt (or type 'quit' to exit):");
                    Console.Write("> ");
                    inputText = Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(inputText) || inputText.ToLower() == "quit")
                        break;

                    Console.WriteLine("Generating image...");

                    try
                    {
                        // Generate image using Azure OpenAI 2.0 API
                        var response = await imageClient.GenerateImageAsync(inputText, new ImageGenerationOptions()
                        {
                            Size = GeneratedImageSize.W1024xH1024,
                            Quality = GeneratedImageQuality.Standard,
                            ResponseFormat = GeneratedImageFormat.Uri
                        });

                        var generatedImage = response.Value;

                        if (generatedImage != null)
                        {
                            imageCount++;

                            // Show revised prompt if available
                            if (!string.IsNullOrEmpty(generatedImage.RevisedPrompt))
                            {
                                Console.WriteLine($"Revised prompt: {generatedImage.RevisedPrompt}");
                            }

                            // Save the image
                            string fileName = $"image_{imageCount:D3}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                            var firstImagePath = await SaveImage(generatedImage.ImageUri, fileName);

                            // trying to use GenerateImageEditAsync; not working yet.
                            //var result2 = await imageClient.GenerateImageEditAsync(firstImagePath, "add a huge frog in the background", new ImageEditOptions()
                            //{
                            //    Size = GeneratedImageSize.W1024xH1024,
                            //    ResponseFormat = GeneratedImageFormat.Uri,
                            //});

                            //await SaveImage(result2.Value.ImageUri, fileName+".edited.png");
                        }
                    }
                    catch (RequestFailedException ex)
                    {
                        Console.WriteLine($"API Error: {ex.Message}");
                        if (ex.Status == 400)
                        {
                            Console.WriteLine("This might be due to content policy restrictions or invalid prompt.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Unexpected error: {ex.Message}");
                    }

                    Console.WriteLine();
                }

                Console.WriteLine("Thank you for using DALL-E Image Generator!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Configuration Error: {ex.Message}");
                Console.WriteLine("Please check your appsettings.json file and Azure OpenAI setup.");
            }
            finally
            {
                httpClient.Dispose();
            }
        }

        static async Task<string> SaveImage(Uri imageUrl, string fileName)
        {
            try
            {
                // Create images directory if it doesn't exist
                string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "images");
                Directory.CreateDirectory(folderPath);
                string filePath = Path.Combine(folderPath, fileName);

                Console.WriteLine("Downloading image...");

                // Download and save the image
                byte[] imageBytes = await httpClient.GetByteArrayAsync(imageUrl);
                await File.WriteAllBytesAsync(filePath, imageBytes);

                Console.WriteLine($"✓ Image saved: {filePath}");
                Console.WriteLine($"  File size: {imageBytes.Length:N0} bytes");

                return filePath;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Download error: {ex.Message}");
                return string.Empty;
            }
            catch (IOException ex)
            {
                Console.WriteLine($"File save error: {ex.Message}");
                return string.Empty;
            }
        }
    }
}