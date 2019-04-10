using System;
using System.Collections.Generic;
using System.Net;

using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

using NeoLABNcodeSDK;
using System.Drawing;
using Amazon.S3;
using Amazon;
using Amazon.S3.Model;
using System.Diagnostics;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

// https://andrewlock.net/exploring-the-net-core-2-1-docker-files-dotnet-runtime-vs-aspnetcore-runtime-vs-sdk/#2-microsoft-dotnet-2-1-0-runtime

namespace Ncode
{

    public class Functions
    {
        /// <summary>
        /// Default constructor that Lambda will invoke.
        /// </summary>
        /// 

        private String localFolder = "/tmp/";

        public Functions()
        {
            if (Environment.GetEnvironmentVariable("AWS_EXECUTION_ENV") == "AWS_DOTNET_LAMDBA_TEST_TOOL_0.9.2")
            {
                localFolder = "E:\\tmp\\";
            }

            CNcodeSDK sdk = new CNcodeSDK();
            Console.WriteLine("Ncode SDK version : " + sdk.GetVersion());


            Console.WriteLine("1) Initializing with app key");
            Console.WriteLine();

            // this is sample app key for testing
            //if (sdk.Init("juyhgt54redfv7ujmnhgt5esq0poli") == false)
            if (sdk.Init("juyhgt54redfv7ujmnhgt5esq0poli", localFolder, localFolder) == false)
            {
                Console.WriteLine("   Error message : " + sdk.GetLastError());
                Console.ReadLine();
                return;
            }


            Console.WriteLine("2) Getting tickets list (optional)");
            Console.WriteLine();
            List<TicketInfo> tickets = sdk.GetTickets();
            if (tickets == null)
            {
                Console.WriteLine("   Error message : " + sdk.GetLastError());
                Console.ReadLine();
                return;
            }

            Console.WriteLine("   Found " + tickets.Count + " ticket(s)");
            Console.WriteLine();

            ////// notice
            // section 44 is intented for certain firmware.
            // Your neo smartpen may not recognize section 44 code.

            for (int i = 0; i < tickets.Count; ++i)
            {
                Console.WriteLine("   Ticket[" + i.ToString() + "]");
                if (tickets[i].ncodeType == NCODE_TYPE.N3C6)
                    Console.WriteLine("   Type    : N3C6");
                else if (tickets[i].ncodeType == NCODE_TYPE.G3C6)
                    Console.WriteLine("   Type    : G3C6");
                else if (tickets[i].ncodeType == NCODE_TYPE.S1C6)
                    Console.WriteLine("   Type    : S1C6");
                else if (tickets[i].ncodeType == NCODE_TYPE.P1C6)
                    Console.WriteLine("   Type    : P1C6");
                Console.WriteLine("   Section : " + tickets[i].section.ToString());
                Console.WriteLine("   Owner   : " + tickets[i].ownerStart.ToString() + "~" + (tickets[i].ownerStart + tickets[i].ownerSize - 1).ToString());
                Console.WriteLine("   Book    : " + tickets[i].bookStart.ToString() + "~" + (tickets[i].bookStart + tickets[i].bookSize - 1).ToString());
                Console.WriteLine("   Page    : " + tickets[i].pageStart.ToString() + "~" + (tickets[i].pageStart + tickets[i].pageSize - 1).ToString());
                Console.WriteLine("   Info    : " + tickets[i].extraInfo);
                Console.WriteLine("   Period  : " + tickets[i].period);

                Console.WriteLine();
            }



            Console.WriteLine("3) Choose ticket and set start page (optional)");
            Console.WriteLine();

            int ticketIndex = 1;
            int ownerOffset = 0;
            int bookOffset = 0;
            int pageOffset = 0;
            TicketInfo startPageInfo = sdk.SetStartPageFromTicket(tickets[ticketIndex], ownerOffset, bookOffset, pageOffset);

            if (startPageInfo == null)
            {
                Console.WriteLine("   Ticket range error");
                Console.WriteLine("   Error message : " + sdk.GetLastError());
                Console.ReadLine();
                return;
            }
            Console.WriteLine("   Selected ticket index : " + ticketIndex.ToString());
            Console.WriteLine("   Owner offset : " + ownerOffset.ToString());
            Console.WriteLine("   Book offset : " + bookOffset.ToString());
            Console.WriteLine("   Page offset : " + pageOffset.ToString());
            Console.WriteLine();



            Console.WriteLine("4) Set size for inch from paper name (optional)");
            Console.WriteLine();
            string paperSizeName = "A4";
            SizeF pageSize = sdk.GetInchValueFromPaperName(paperSizeName, false);
            Console.WriteLine("   Paper Size (" + paperSizeName + ") : " + "(" + pageSize.Width.ToString() + ", " + pageSize.Height.ToString() + ")");
            Console.WriteLine();



            Console.WriteLine("5) Generating Ncode data");
            Console.WriteLine();
            List<NcodePage> codeData = new List<NcodePage>();
            int pageCount = 1;

            if (sdk.GenerateNcode(
                out codeData,
                startPageInfo,
                pageSize.Width,     // inch
                pageSize.Height,    // inch
                pageCount) != 0)
            {
                Console.WriteLine("   Error message : " + sdk.GetLastError());
                Console.ReadLine();
                return;
            }

            // You can also create Ncode data via entering code informaion directly.
            // Use it when you do not need to inquiry tickets and you know exactly what code information you need.
            //
            //if (sdk.GenerateNcode(
            //    out codeData,
            //    CNcodeSDK.NCODE_TYPE.N3C6,  // Ncode type
            //    3,                          // section
            //    28,                         // owner
            //    10,                         // book
            //    1,                          // page
            //    6.0,                        // inch
            //    8.0,                        // inch
            //    pageCount) != 0)
            //{
            //    Console.WriteLine("   Error message : " + sdk.GetLastError());
            //    Console.ReadLine();
            //    return;
            //}


            Console.WriteLine("5-1) Saving Ncode image file");
            Console.WriteLine();

            // When you generate N3C6 or G3C6 code image, you can select dot type, "dot" or "line".
            // If you set "true", it generate "dot" code image.
            // If you set "false", it generate "line" code image.
            // S1C6, P1C6 and postscript output use just only "dot" code.

            sdk.SetDotType(true);

            {
                string outputFilename = string.Format("{0}_{1}_{2}_{3}-{4}",
                        codeData[0].section.ToString(),
                        codeData[0].owner.ToString(),
                        codeData[0].book.ToString(),
                        codeData[0].page.ToString(),
                        codeData.Count - 1);
                if (sdk.GetPostscript(codeData, localFolder + outputFilename + ".ps") != 0)
                {
                    Console.WriteLine("   Error message : " + sdk.GetLastError());
                    Console.WriteLine("   Error message : Temporarily support only S1C6 and P1C6.");
                    return;
                }


                // https://www.pdflabs.com/tools/pdftk-server/
                // https://www.pdflabs.com/docs/pdftk-man-page/#dest-op-background
                // https://lob.com/blog/aws-lambda-pdftk
                // pdftk scoot.pdf background dots_manual_convert.pdf output out_backgorund.pdf


                string args = " " + localFolder + outputFilename + ".ps " + localFolder + outputFilename + ".pdf";
                Console.WriteLine("   args : " + args);

                Bash("ps2pdf", args);

                args = " " + "scoot.pdf background " + localFolder + outputFilename + ".pdf output " + localFolder + outputFilename + "_background.pdf";
                Console.WriteLine("   args : " + args);
                Bash("pdftk", args);

                Console.WriteLine("   done applying pattern to background ");

                SendImageToS3(outputFilename + "_background.pdf", localFolder + outputFilename + "_background.pdf");
            }
        }

        public static string Bash(string cmd, string args)
        {
            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = cmd,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            string result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return result;
        }

        // https://csharp.hotexamples.com/examples/Amazon.S3/AmazonS3Client/PutObject/php-amazons3client-putobject-method-examples.html
        // https://docs.aws.amazon.com/sdkfornet1/latest/apidocs/html/T_Amazon_S3_AmazonS3Client.htm
        public static Boolean SendImageToS3(String key, String filePath)
        {
            var success = false;

            using (var client = new AmazonS3Client(RegionEndpoint.APSoutheast1))
            {
                try
                {
                    Console.WriteLine("uploading " + filePath);
                    PutObjectRequest request = new PutObjectRequest()
                    {
                        FilePath = filePath,
                        BucketName = "fs-background-images",
                        Key = key
                    };

                    client.PutObjectAsync(request);
                    success = true;
                }

                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            return success;
        }

        /// <summary>
        /// A Lambda function to respond to HTTP Get methods from API Gateway
        /// </summary>
        /// <param name="request"></param>
        /// <returns>The list of blogs</returns>
        public APIGatewayProxyResponse Get(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogLine("Get Request\n");

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = "Hello AWS Serverless",
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
            };

            return response;
        }
    }
}
