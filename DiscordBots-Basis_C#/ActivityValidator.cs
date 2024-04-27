using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;

namespace C_
{
    public class ActivityValidator
    {
        private readonly string file_path;
        private readonly string schemaJson = @"
        {
            'type': 'object',
            'properties': {
                'activity_type': {
                    'type': 'string',
                    'enum': ['Playing', 'Streaming', 'Listening', 'Watching', 'Competing', '']
                },
                'activity_title': {'type': 'string'},
                'activity_url': {'type': 'string'},
                'status': {
                    'type': 'string',
                    'enum': ['online', 'idle', 'dnd', 'invisible']
                }
            }
        }";

        private readonly dynamic defaultContent = new
        {
            activity_type = "Playing",
            activity_title = "Made by Serpensin: https://gitlab.bloodygang.com/Serpensin",
            activity_url = "",
            status = "online"
        };

        public ActivityValidator(string file_path)
        {
            this.file_path = file_path;
        }

        public void ValidateAndFixJson()
        {
            if (File.Exists(file_path))
            {
                try
                {
                    string data = File.ReadAllText(file_path);
                    JSchema schema = JSchema.Parse(schemaJson);
                    JObject jsonData = JObject.Parse(data);
                    if (!jsonData.IsValid(schema, out IList<string> errors))
                    {
                        WriteDefaultContent();
                    }
                }
                catch (JsonReaderException jre)
                {
                    Console.WriteLine($"JsonReaderException: {jre.Message}");
                    WriteDefaultContent();
                }
            }
            else
            {
                WriteDefaultContent();
            }
        }

        private void WriteDefaultContent()
        {
            string defaultJson = JsonConvert.SerializeObject(defaultContent, Formatting.Indented);
            File.WriteAllText(file_path, defaultJson);
        }
    }
}
