using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using WebInterface.Models;

namespace WebInterface.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View("_Layout");
        }

        public IActionResult Home()
        {
            return View();
        }

        public IActionResult Applications()
        {
            return View();
        }

        public IActionResult Policies()
        {
            /*
            ViewData["Message"] = "Your application description page.";

            return View();*/
            return View();
        }

        public IActionResult Status()
        {
            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public IActionResult PolicyModal()
        {
            return View();
        }

        public IActionResult Modal()
        {
            return View();
        }

        public IActionResult StatusModal()
        {
            return View();
        }

        public IActionResult ConfigureModal()
        {
            return View();
        }

        public IActionResult ServiceConfigureModal()
        {
            return View();
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }


        [Route("[controller]/api/policies/{cs}")]
        [HttpGet]
        public async Task<IActionResult> GetPolicies(String cs)
        {
            string URL = "http://" + cs + "/BackupRestore/BackupPolicies";
            string urlParameters = "?api-version=6.2-preview";
            HttpClient client = new HttpClient
            {
                BaseAddress = new Uri(URL)
            };
            client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

            // List data response.
            HttpResponseMessage response = await client.GetAsync(urlParameters);  // Blocking call!
            if (response.IsSuccessStatusCode)
            {
                // Parse the response body. Blocking!
                var content = response.Content.ReadAsAsync<JObject>().Result;
                JArray array = (JArray)content["Items"];
                List<String> policies = new List<string>();
                foreach (var item in array)
                {
                    policies.Add(item["Name"].ToString());
                }
                return this.Json(policies);
            }
            else
            {
                Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
                return null;
            }

        }
    }
}
