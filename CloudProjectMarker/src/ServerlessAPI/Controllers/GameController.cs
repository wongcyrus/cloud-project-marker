using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ProjectTestLib.Helper;

namespace ServerlessAPI.Controllers
{
    [DataContract]
    public class GameTaskData
    {
        [DataMember] public int GameClassOrder { get; set; }
        [DataMember] public string Name { get; set; }

        [DataMember] public string[] Tests { get; set; }
        [DataMember] public string Instruction { get; set; }
        [DataMember] public string Filter { get; set; }
        [DataMember] public int TimeLimit { get; set; }
        [DataMember] public int Reward { get; set; }

        public override string ToString()
        {
            return Name + "," + GameClassOrder + "," + TimeLimit + "," + Reward + "," + Filter + "=>" + Instruction.Substring(0, 30);
        }
    }

    [Route("api/[controller]")]
    [Produces("application/json")]
    public class GameController : ControllerBase
    {
        private readonly ILogger<GraderController> logger;
        public GameController(ILogger<GraderController> logger)
        {
            this.logger = logger;
        }

        // GET: api/Game
        [HttpGet]
        public IActionResult Get()
        {
            // TODO: Implement logic to retrieve all games
            return Ok(GetTasksJson());
        }

        private IEnumerable<Type> GetTypesWithHelpAttribute(Assembly assembly)
        {
            return from Type type in assembly!.GetTypes()
                   where type.GetCustomAttributes(typeof(GameClassAttribute), true).Length > 0
                   select type;
        }

        public string GetTasksJson()
        {
            {
                var assembly = Assembly.GetAssembly(type: typeof(GameClassAttribute));
                var allTasks = new List<GameTaskData>();
                foreach (var testClass in GetTypesWithHelpAttribute(assembly!))
                {
                    var gameClass = testClass.GetCustomAttribute<GameClassAttribute>();
                    var tasks = testClass.GetMethods().Where(m => m.GetCustomAttribute<GameTaskAttribute>() != null)
                        .Select(c => new { c.Name, GameTask = c.GetCustomAttribute<GameTaskAttribute>()! });

                    var independentTests = tasks.Where(c => c.GameTask.GroupNumber == -1)
                        .Select(c => new GameTaskData()
                        {
                            Name = testClass.FullName + "." + c.Name,
                            Tests = [testClass.FullName + "." + c.Name],
                            GameClassOrder = gameClass!.Order,
                            Instruction = c.GameTask.Instruction,
                            Filter = "test=" + testClass.FullName + "." + c.Name,
                            Reward = c.GameTask.Reward,
                            TimeLimit = c.GameTask.TimeLimit
                        });


                    var groupedTasks = tasks.Where(c => c.GameTask.GroupNumber != -1)
                        .GroupBy(c => c.GameTask.GroupNumber)
                        .Select(c =>
                            new GameTaskData()
                            {
                                Name = string.Join(" ", c.Select(a => testClass.FullName + "." + a.Name)),
                                Tests = c.Select(a => testClass.FullName + "." + a.Name).ToArray(),
                                GameClassOrder = gameClass!.Order,
                                Instruction = string.Join("", c.Select(a => a.GameTask.Instruction)),
                                Filter =
                                    string.Join("||", c.Select(a => "test==\"" + testClass.FullName + "." + a.Name + "\"")),
                                Reward = c.Sum(a => a.GameTask.Reward),
                                TimeLimit = c.Sum(a => a.GameTask.TimeLimit),
                            }
                        );

                    allTasks.AddRange(independentTests);
                    allTasks.AddRange(groupedTasks);
                }

                var allCompletedTask = allTasks.ToList();
                var serializerSettings = new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                };
                allCompletedTask = allCompletedTask.OrderBy(c => c.GameClassOrder).ThenBy(c => c.Tests.First()).ToList();
                var json = JsonConvert.SerializeObject(allCompletedTask.ToArray(), serializerSettings);
                return json;
            }
        }
    }
}
