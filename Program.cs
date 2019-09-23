using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using MagazineSubscriptions.Objects;
using Newtonsoft.Json;


namespace MagazineSubscriptions
{
    class Program
    {


        private static string C_SITE = "http://magazinestore.azurewebsites.net";
        private static string C_REQUEST_TOKEN = "/api/token";                            //This will return a token that must be used for all.subsequent calls to the API.
        private static string C_REQUEST_CATEGORIES = "/api/categories/{0}";         //This endpoint will return our current magazine categories
        private static string C_REQUEST_MAGAZINES = "/api/magazines/{0}/{1}"; // This endpoint will return magazines for the specified category
        private static string C_REQUEST_SUBSCRIBERS = "/api/subscribers/{0}";       //This endpoint will return our current subscribers along withthe magazine ids that they are subscribed to.
        private static string C_POST_ANSWER = "/api/answer/{0}";                   // You will have to POST your answer to this endpoint.
        private static List<Magazine> fullMagazineList = new List<Magazine>();
        private static SubscriberResponse subscribers;
        string RESULT_TOKEN = string.Empty;
      

        static void Main(string[] args)
        {
            RunProgram();
            while(Console.ReadKey().Key!=ConsoleKey.Enter)
            {
                RunProgram();
            }

            
        }

        private static void RunProgram()
        {
            WebClient client = new WebClient();
            fullMagazineList = new List<Magazine>();
            subscribers = null;


            //Step 1 - Get Token.  All other steps are dependent on this, so  no need to
            //         run asynchronously.
            TokenResponse tokenResponseObject = GetToken(client);
            //Step 2 - Get List of subscribers.  This has no dependencies so 
            //         run it asynchronously
            Task subscriberTask = GetSubscribersAsync(tokenResponseObject.token);
            //Step 3 - Get list of categories.  The magazine list needs to use the category
            //         list, so no benefit to running asynchronously.
            CategoryListResponse categories = GetCategoryList(client, tokenResponseObject.token);
            //Step 4 - Get list of magazines.  This method is synchronous, but will create
            //         a task to asynchronously get results for each category.
            GetMagazineList(categories, tokenResponseObject.token);

            //Wait for the asynchronous task to complete before building the answer.
            subscriberTask.Wait();

            //Build the answer
            int categoryCount = categories.data.Length;
            Answer answer = BuildAnswer(categoryCount, subscribers);
            //Post the answer and write out the response.
            PostAnswer(answer, tokenResponseObject.token);

            Console.WriteLine("Job has completed. Press <Enter> to end, or <Tab> to run again.");
        }

        /// <summary>
        /// Calls the token api and returns the token needed for rest of the calls
        /// </summary>
        /// <param name="client">Webclient object</param>
        /// <returns>TokenRespones object with token to use for all future calls.</returns>
        private static TokenResponse GetToken(WebClient client)
        {
            String tokenRequest = String.Concat(C_SITE, C_REQUEST_TOKEN);
            var tokenResponse = client.DownloadString(tokenRequest);

            TokenResponse tokenResponseObject = (TokenResponse)JsonConvert.DeserializeObject(tokenResponse, typeof(TokenResponse));
            Console.WriteLine(tokenResponse);
            return tokenResponseObject;
        }

        /// <summary>
        /// Return a list of categories.
        /// </summary>
        /// <param name="client">webclient object</param>
        /// <param name="token">the token string returned with the first call.</param>
        /// <returns>a CaegoryListResponse object with an array of categories</returns>
        private static CategoryListResponse GetCategoryList(WebClient client, string token)
        {
            string categoryListRequest = string.Concat(C_SITE, String.Format(C_REQUEST_CATEGORIES, token));
            var categoryListResponse = client.DownloadString(categoryListRequest);
            CategoryListResponse categories = (CategoryListResponse)JsonConvert.DeserializeObject(categoryListResponse, typeof(CategoryListResponse));
            return categories;
        }

        /// <summary>
        /// Makes an asynchronous call for magazines for each category in list
        /// </summary>
        /// <param name="categories">CategoryListResponse object with list of category names</param>
        /// <param name="client"></param>
        /// <param name="tokenString"></param>
        private static void GetMagazineList(CategoryListResponse categories,  string tokenString)
        {
            using (var httpClient = new HttpClient())
            {
                List<Task> taskList = new List<Task>();
                foreach (string categoryName in categories.data)
                {
                    string magazineRequest = string.Concat(C_SITE, string.Format(C_REQUEST_MAGAZINES, tokenString, categoryName));
                    Task newTask= GetMagazineListForCategoryAsync(httpClient, magazineRequest);
                    taskList.Add(newTask);
                }

                //We created a task for each category.  
                //wait for them all to complete before we exit the procedure
                foreach(Task t in taskList)
                {
                    t.Wait();
                }
            }
        }


        /// <summary>
        /// Asynchronous call to get a magazine list for a single category.
        /// </summary>
        /// <param name="httpClient"></param>
        /// <param name="magazineRequest"></param>
        /// <returns></returns>
        private static async Task GetMagazineListForCategoryAsync(HttpClient httpClient, string magazineRequest)
        {
            string magazineResponse = await httpClient.GetStringAsync(magazineRequest);
            MagazineResponse magazines = (MagazineResponse)JsonConvert.DeserializeObject(magazineResponse, typeof(MagazineResponse));
            fullMagazineList.AddRange(magazines.data);
        }

        /// <summary>
        /// Asynchronous call to get a list of subscribers
        /// </summary>
        /// <param name="token">token string retrieved at beginning of run</param>
        /// <returns>Task.  Also populated the subscribers object.</returns>
        private static async Task GetSubscribersAsync(string token)
        {
            using (var httpClient = new HttpClient())
            {
                string subscriberRequest = string.Concat(C_SITE, String.Format(C_REQUEST_SUBSCRIBERS, token));
                var subscriberResponse = await httpClient.GetStringAsync(subscriberRequest);
                subscribers = (SubscriberResponse)JsonConvert.DeserializeObject(subscriberResponse, typeof(SubscriberResponse));
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="categoryCount"></param>
        /// <param name="subscribers"></param>
        /// <returns></returns>
        private static Answer BuildAnswer(int categoryCount, SubscriberResponse subscribers)
        {
            Answer answer = new Answer();
            List<string> fullSubscribers = new List<string>();


            foreach (Subscriber s in subscribers.data)
            {
                //This linq statement gets the distinct list of categories
                // in this subscriber's list, then gets the count.

                var subScriberCatCount = (from cc in s.magazineIds
                                          join mag in fullMagazineList
                                          on cc equals mag.id
                                          select mag.category).Distinct().Count();

                //If the count of this subscriber's categories = the total count of 
                //subscriber categories, add to the list.
                if (subScriberCatCount == categoryCount)
                {
                    fullSubscribers.Add(s.id);
                }
            }
            answer.description = "Answer Description";
            answer.subscribers = fullSubscribers.ToArray();
            return answer;
        }

        /// <summary>
        /// Post the answer that was built, and write the response
        /// </summary>
        /// <param name="answer">The answer object previously built with list of subscribers</param>
        /// <param name="token">token string from first request.</param>
        private static void PostAnswer(Answer answer, string token)
        {
            string postURL = string.Concat(C_SITE, String.Format(C_POST_ANSWER, token));
            string postData = JsonConvert.SerializeObject(answer);

            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(postURL);
            webRequest.Method = "POST";
            webRequest.ContentType = "application/json";
            byte[] postBytes = Encoding.ASCII.GetBytes(postData);
            webRequest.ContentLength = postBytes.Length;
            Stream requestStream = webRequest.GetRequestStream();

            // now send it
            requestStream.Write(postBytes, 0, postBytes.Length);
            requestStream.Close();
            WebResponse finalResponse = webRequest.GetResponse();

            Stream receiveStream = finalResponse.GetResponseStream();
            Encoding encode = System.Text.Encoding.GetEncoding("utf-8");
            // Pipes the stream to a higher level stream reader with the required encoding format. 
            StreamReader readStream = new StreamReader(receiveStream, encode);
            string resultString = readStream.ReadToEnd();
            Console.WriteLine("Final Result");
            Console.WriteLine(resultString);
        }
    }
}

