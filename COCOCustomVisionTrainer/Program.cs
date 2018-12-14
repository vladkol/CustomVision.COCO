using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Cognitive.CustomVision.Helpers;
using Microsoft.Cognitive.CustomVision.Helpers.COCO;
using COCOAPI;
using Microsoft.Extensions.CommandLineUtils;

namespace COCOCustomVisionTrainer
{
    class Program
    {
        // Current Custom Vision limit for Free and F0 tiers is 5000, S0 tiers has 50000 limit 
        private const uint customVisionImageLimit = 5000;

        static int Main(string[] args)
        {
            CommandLineApplication commandLineApplication = new CommandLineApplication(false)
            {
                Name = "COCOCustomVisionTrainer",
                Description = "[Trains Azure Custom Vision projects using COCO dataset]",
                FullName = "Azure Custom Vision COCO Traner"
            };

            CommandOption trainingKey = commandLineApplication.Option(
              "-tkey | --trainingKey", 
              "Provide your Custom Vision training key (https://www.customvision.ai/projects#/settings) as -tkey | --trainingKey YOUR_KEY",
              CommandOptionType.SingleValue);
            CommandOption filePath = commandLineApplication.Option(
              "-f | --fileDatasetJSON",
              "Provide a path or URL of a COCO Train/Val annonations JSON file as -f | --fileDatasetJSON instances_train*.json or instances_val*.json. " +
              "Download them from http://cocodataset.org/#download",
              CommandOptionType.SingleValue);;
            CommandOption projectName = commandLineApplication.Option(
              "-p | --projectName",
              "Provide Custom Vision project name as -p | --projectName YOUR_PROJECT_NAME",
              CommandOptionType.SingleValue);
            CommandOption isDetectionModel = commandLineApplication.Option(
              "-d | --detection",
              "Specify if it is a Detection project",
              CommandOptionType.NoValue);
            CommandOption train = commandLineApplication.Option(
              "-t | --train",
              "Specify for training the project after uploading images",
              CommandOptionType.NoValue);

            CommandOption categories = commandLineApplication.Option(
              "-c | --categories",
              "COCO categories or supercategories to use. If no categories specified, training on all categories",
              CommandOptionType.MultipleValue);

            CommandOption numberOfImages = commandLineApplication.Option(
              "-i | --images",
              "Maximum number of images across categories -i | --images. If no number specified, using all images with objects of categories.",
              CommandOptionType.SingleValue);

            commandLineApplication.HelpOption("-? | -h | --help");
            commandLineApplication.OnExecute(() =>
            {
                if (trainingKey.HasValue() && filePath.HasValue() && projectName.HasValue())
                {
                    List<string> categoriesList = new List<string>();
                    uint numberOfImagesToUse = 0;

                    if(categories.HasValue())
                    {
                        var categoriesText = categories.Value().Replace(", ", ",");
                        categoriesList = categoriesText.Split(',').Select(c => c.Trim()).Where(ct => ct.Length > 0).ToList();
                    }
                    if(numberOfImages.HasValue())
                    {
                        if(!UInt32.TryParse(numberOfImages.Value().Trim(), out numberOfImagesToUse))
                        {
                            Console.WriteLine("Number of images option is invalid, must be a positive integer number.\n");
                            commandLineApplication.ShowHelp();
                            return 2;
                        }
                    }

                    Console.WriteLine(commandLineApplication.FullName);
                    Console.WriteLine(commandLineApplication.Description);

                    try
                    {
                        var t = WorkOnProject(trainingKey.Value(), projectName.Value(), filePath.Value(),
                            categoriesList, numberOfImagesToUse,
                            isDetectionModel.HasValue(), train.HasValue());
                        t.Wait();
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine("\n\nERROR when working on a project!\n\n{0}, Message: {1}", ex.GetType().Name, ex.Message);
                        if(ex.InnerException != null)
                        {
                            Console.WriteLine();
                            Console.WriteLine("Inner exception {0}\n\nMessage: {1}", ex.GetType().Name, ex.Message);
                            return 3;
                        }
                    }

                    Console.WriteLine("\n============ DONE ============\n");

                    return 0;
                }
                else
                {
                    Console.WriteLine("You must provide traning key, project name, and dataset JSON file.");
                    commandLineApplication.ShowHelp();
                    return 1;
                }
                
            });

            return commandLineApplication.Execute(args);
        }

        static async Task WorkOnProject(string trainingKey, string projectName, string COCOInstancesFilePathOrUrl, IList<string> categories, uint numberOfImages, bool isDetectionModel, bool train)
        {
            Console.Write($"\nLoading, parsing and preparing {COCOInstancesFilePathOrUrl}...");

            if (numberOfImages == 0 || numberOfImages > customVisionImageLimit)
            {
                numberOfImages = 50000;
            }

            var data = await COCODatasetFactory.LoadFromCOCOAnnotationJSONFileAsync(COCOInstancesFilePathOrUrl, categories, numberOfImages);
            Console.WriteLine("done.");

            Console.WriteLine("Prepared COCO dataset with {0} categories and {1} images.\n", data.categoriesAndIds.Count, data.traningSet.Count);

            Console.Write($"Initializing Custom Vision Project {COCOInstancesFilePathOrUrl}...");
            ProjectTraningConfiguration trainingConfig = await ProjectTraningConfiguration.CreateProjectAsync(trainingKey, projectName, true, isDetectionModel);
            Console.WriteLine("done.");

            Console.Write($"\nLoading traning images to Custom Vision Project...");
            CustomVisionProjectTraning traning = new CustomVisionProjectTraning(trainingConfig);

            int ignoredImages = await traning.LoadWithCOCO(data, isDetectionModel);

            if (ignoredImages == 0)
                Console.WriteLine("done.");
            else
                Console.WriteLine($"done. Ignored {ignoredImages} image(s).");

            if (train)
            {
                Console.Write($"\nTraning Custom Vision Project...");
                await traning.Train();
                Console.WriteLine("done.");
            }
            else
            {
                Console.WriteLine("Traning is not requested.");
            }
 
        }
    }
}
