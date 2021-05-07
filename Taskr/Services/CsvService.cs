namespace Taskr
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;

    using CsvHelper;
    using CsvHelper.Configuration;

    internal static class CsvService
    {
        public static IEnumerable<T> GetRecords<T>(Stream file)
        {
            var textReader = new StreamReader(file);
            using (var csvReader = new CsvReader(textReader, new CsvConfiguration(CultureInfo.InvariantCulture)))
            {
                var results = csvReader.GetRecords<T>().ToList();
                return results;
            }
        }
    }
}
