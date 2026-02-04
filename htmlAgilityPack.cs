using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Newtonsoft.Json;

namespace HHParser
{
    class Program
    {
        static readonly HttpClient client = new HttpClient();
        static readonly Dictionary<string, List<string>> techKeywords = new Dictionary<string, List<string>>
        {
            { "C#", new List<string> { "c#", ".net", "asp.net", "entity framework" } },
            { "Python", new List<string> { "python", "django", "flask", "fastapi" } },
            { "Java", new List<string> { "java", "spring", "hibernate", "j2ee" } },
            { "JavaScript", new List<string> { "javascript", "js", "ecmascript", "vanilla js" } },
            { "TypeScript", new List<string> { "typescript", "ts" } },
            { "PHP", new List<string> { "php", "laravel", "symfony" } },
            { "Go", new List<string> { "go", "golang" } },
            { "SQL", new List<string> { "sql", "postgresql", "mysql", "mssql", "oracle" } },
            { "NoSQL", new List<string> { "mongodb", "redis", "cassandra", "elasticsearch" } },
            { "React", new List<string> { "react", "react.js", "redux" } },
            { "Angular", new List<string> { "angular", "angular.js" } },
            { "Vue", new List<string> { "vue", "vue.js", "vuex" } },
            { "Docker", new List<string> { "docker", "container" } },
            { "Kubernetes", new List<string> { "kubernetes", "k8s" } },
            { "AWS", new List<string> { "aws", "amazon web services", "s3", "ec2" } },
            { "Azure", new List<string> { "azure" } },
            { "Linux", new List<string> { "linux", "ubuntu", "centos" } },
            { "Git", new List<string> { "git", "github", "gitlab" } },
            { "CI/CD", new List<string> { "jenkins", "gitlab ci", "github actions", "teamcity" } },
            { "Terraform", new List<string> { "terraform", "iac" } },
            { "Kafka", new List<string> { "kafka" } },
            { "RabbitMQ", new List<string> { "rabbitmq" } },
            { "Grafana", new List<string> { "grafana" } },
            { "Prometheus", new List<string> { "prometheus" } },
            { "Tableau", new List<string> { "tableau", "power bi" } },
            { "Spark", new List<string> { "spark", "apache spark" } },
            { "Hadoop", new List<string> { "hadoop", "hdfs" } }
        };

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Парсинг вакансий с hh.ru ===");
            
            try
            {
                // Устанавливаем заголовки
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                client.DefaultRequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");

                var allVacancies = new List<Vacancy>();
                var technologies = new Dictionary<string, int>();
                var employmentTypes = new Dictionary<string, int>();
                var salariesByExperience = new Dictionary<string, List<int>>();

                // Парсим несколько страниц
                for (int page = 0; page < 10; page++) // 10 страниц = ~200 вакансий
                {
                    Console.WriteLine($"Парсинг страницы {page + 1}...");
                    
                    var vacancies = await ParseHHPage(page);
                    allVacancies.AddRange(vacancies);
                    
                    // Анализируем вакансии
                    foreach (var vacancy in vacancies)
                    {
                        // 1. Анализ технологий
                        AnalyzeTechnologies(vacancy.Description, technologies);
                        
                        // 2. Подсчет типов занятости
                        if (!string.IsNullOrEmpty(vacancy.EmploymentType))
                        {
                            if (employmentTypes.ContainsKey(vacancy.EmploymentType))
                                employmentTypes[vacancy.EmploymentType]++;
                            else
                                employmentTypes[vacancy.EmploymentType] = 1;
                        }
                        
                        // 3. Зарплата по опыту
                        if (vacancy.SalaryFrom > 0 && !string.IsNullOrEmpty(vacancy.Experience))
                        {
                            var expKey = GetExperienceLevel(vacancy.Experience);
                            if (!salariesByExperience.ContainsKey(expKey))
                                salariesByExperience[expKey] = new List<int>();
                            
                            salariesByExperience[expKey].Add(vacancy.SalaryFrom);
                        }
                    }
                    
                    await Task.Delay(2000); // Задержка между запросами
                }

                // Выводим результаты
                Console.WriteLine("\n=== РЕЗУЛЬТАТЫ ===");
                Console.WriteLine($"Всего вакансий: {allVacancies.Count}");
                
                DisplayTopTechnologies(technologies);
                DisplayEmploymentTypes(employmentTypes);
                DisplaySalariesByExperience(salariesByExperience);
                
                // Сохраняем в файлы
                SaveResults(technologies, employmentTypes, salariesByExperience);
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
        }

        static async Task<List<Vacancy>> ParseHHPage(int page)
        {
            var vacancies = new List<Vacancy>();
            
            // URL для IT вакансий на hh.ru
            var url = $"https://hh.ru/search/vacancy?text=разработчик&area=1&page={page}&experience=noExperience&experience=between1And3&experience=between3And6&experience=moreThan6";
            
            try
            {
                var response = await client.GetStringAsync(url);
                var doc = new HtmlDocument();
                doc.LoadHtml(response);
                
                // Находим все карточки вакансий
                var vacancyNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'vacancy-serp-item')]");
                
                if (vacancyNodes != null)
                {
                    foreach (var node in vacancyNodes)
                    {
                        var vacancy = ParseVacancyCard(node);
                        if (vacancy != null)
                            vacancies.Add(vacancy);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при парсинге страницы {page}: {ex.Message}");
            }
            
            return vacancies;
        }

        static Vacancy ParseVacancyCard(HtmlNode node)
        {
            try
            {
                var vacancy = new Vacancy();
                
                // Название вакансии
                var titleNode = node.SelectSingleNode(".//a[contains(@class, 'bloko-link')]");
                if (titleNode != null)
                {
                    vacancy.Title = titleNode.InnerText.Trim();
                    vacancy.Url = "https://hh.ru" + titleNode.GetAttributeValue("href", "");
                }
                
                // Компания
                var companyNode = node.SelectSingleNode(".//a[contains(@data-qa, 'vacancy-serp__vacancy-employer')]");
                if (companyNode != null)
                    vacancy.Company = companyNode.InnerText.Trim();
                
                // Зарплата
                var salaryNode = node.SelectSingleNode(".//span[contains(@data-qa, 'vacancy-serp__vacancy-compensation')]");
                if (salaryNode != null)
                {
                    var salaryText = salaryNode.InnerText.Trim();
                    ParseSalary(salaryText, out int from, out int to);
                    vacancy.SalaryFrom = from;
                    vacancy.SalaryTo = to;
                }
                
                // Местоположение
                var locationNode = node.SelectSingleNode(".//div[contains(@data-qa, 'vacancy-serp__vacancy-address')]");
                if (locationNode != null)
                    vacancy.Location = locationNode.InnerText.Trim();
                
                // Опыт
                var expNode = node.SelectSingleNode(".//span[contains(@data-qa, 'vacancy-serp__vacancy-work-experience')]");
                if (expNode != null)
                    vacancy.Experience = expNode.InnerText.Trim();
                
                // Тип занятости
                var empNode = node.SelectSingleNode(".//div[contains(@data-qa, 'vacancy-serp__vacancy-work-schedule')]");
                if (empNode != null)
                    vacancy.EmploymentType = empNode.InnerText.Trim();
                
                // Описание (парсим отдельную страницу для получения полного описания)
                if (!string.IsNullOrEmpty(vacancy.Url))
                {
                    vacancy.Description = GetVacancyDescription(vacancy.Url).Result;
                }
                
                return vacancy;
            }
            catch (Exception)
            {
                return null;
            }
        }

        static async Task<string> GetVacancyDescription(string url)
        {
            try
            {
                await Task.Delay(1000); // Задержка для соблюдения лимитов
                
                var response = await client.GetStringAsync(url);
                var doc = new HtmlDocument();
                doc.LoadHtml(response);
                
                var descNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'vacancy-description')]");
                if (descNode != null)
                    return descNode.InnerText.ToLower();
                
                return "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        static void ParseSalary(string salaryText, out int salaryFrom, out int salaryTo)
        {
            salaryFrom = 0;
            salaryTo = 0;
            
            try
            {
                // Убираем нецифровые символы, кроме точек и тире
                salaryText = salaryText.Replace(" ", "").Replace("руб.", "").Replace("₽", "");
                
                // Разные форматы зарплат
                if (salaryText.Contains("–") || salaryText.Contains("-"))
                {
                    // Диапазон: "100000-200000"
                    var parts = salaryText.Split(new[] { '–', '-' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        if (int.TryParse(new string(parts[0].Where(char.IsDigit).ToArray()), out int from))
                            salaryFrom = from;
                        if (int.TryParse(new string(parts[1].Where(char.IsDigit).ToArray()), out int to))
                            salaryTo = to;
                    }
                }
                else if (salaryText.Contains("от"))
                {
                    // "от 100000"
                    salaryText = salaryText.Replace("от", "");
                    if (int.TryParse(new string(salaryText.Where(char.IsDigit).ToArray()), out int from))
                        salaryFrom = from;
                }
                else if (salaryText.Contains("до"))
                {
                    // "до 200000"
                    salaryText = salaryText.Replace("до", "");
                    if (int.TryParse(new string(salaryText.Where(char.IsDigit).ToArray()), out int to))
                        salaryTo = to;
                }
                else
                {
                    // Конкретная сумма
                    if (int.TryParse(new string(salaryText.Where(char.IsDigit).ToArray()), out int amount))
                    {
                        salaryFrom = amount;
                        salaryTo = amount;
                    }
                }
            }
            catch (Exception)
            {
                // Игнорируем ошибки парсинга зарплаты
            }
        }

        static void AnalyzeTechnologies(string description, Dictionary<string, int> technologies)
        {
            if (string.IsNullOrEmpty(description))
                return;
                
            foreach (var tech in techKeywords)
            {
                foreach (var keyword in tech.Value)
                {
                    if (description.Contains(keyword))
                    {
                        if (technologies.ContainsKey(tech.Key))
                            technologies[tech.Key]++;
                        else
                            technologies[tech.Key] = 1;
                        break; // Не считаем несколько ключевых слов одной технологии дважды
                    }
                }
            }
        }

        static string GetExperienceLevel(string experienceText)
        {
            experienceText = experienceText.ToLower();
            
            if (experienceText.Contains("без опыта") || experienceText.Contains("нет опыта") || 
                experienceText.Contains("0 лет") || experienceText.Contains("стажер"))
                return "Нет опыта";
            else if (experienceText.Contains("1-3") || experienceText.Contains("1 год") || 
                     experienceText.Contains("до 3 лет"))
                return "Junior (1-3 года)";
            else if (experienceText.Contains("3-6") || experienceText.Contains("от 3 лет"))
                return "Middle (3-6 лет)";
            else if (experienceText.Contains("более 6") || experienceText.Contains("от 6 лет") || 
                     experienceText.Contains("6+"))
                return "Senior (6+ лет)";
            else if (experienceText.Contains("lead") || experienceText.Contains("руковод"))
                return "Lead";
            else
                return "Другой";
        }

        static void DisplayTopTechnologies(Dictionary<string, int> technologies)
        {
            Console.WriteLine("\n=== ТОП-15 ВОСТРЕБОВАННЫХ ТЕХНОЛОГИЙ ===");
            
            var sortedTech = technologies.OrderByDescending(x => x.Value).Take(15);
            
            int rank = 1;
            foreach (var tech in sortedTech)
            {
                Console.WriteLine($"{rank}. {tech.Key}: {tech.Value} вакансий");
                rank++;
            }
        }

        static void DisplayEmploymentTypes(Dictionary<string, int> employmentTypes)
        {
            Console.WriteLine("\n=== РАСПРЕДЕЛЕНИЕ ВАКАНСИЙ ПО ТИПАМ ЗАНЯТОСТИ ===");
            
            var sortedTypes = employmentTypes.OrderByDescending(x => x.Value);
            
            foreach (var type in sortedTypes)
            {
                Console.WriteLine($"{type.Key}: {type.Value} вакансий");
            }
        }

        static void DisplaySalariesByExperience(Dictionary<string, List<int>> salariesByExperience)
        {
            Console.WriteLine("\n=== ЗАРПЛАТЫ ПО ОПЫТУ РАБОТЫ (РУБ.) ===");
            
            foreach (var level in salariesByExperience)
            {
                if (level.Value.Count > 0)
                {
                    var avg = (int)level.Value.Average();
                    var min = level.Value.Min();
                    var max = level.Value.Max();
                    
                    Console.WriteLine($"{level.Key}:");
                    Console.WriteLine($"  • Средняя: {avg:#,##0}");
                    Console.WriteLine($"  • Диапазон: {min:#,##0} - {max:#,##0}");
                    Console.WriteLine($"  • Вакансий: {level.Value.Count}");
                }
            }
        }

        static void SaveResults(Dictionary<string, int> technologies, Dictionary<string, int> employmentTypes, Dictionary<string, List<int>> salariesByExperience)
        {
            try
            {
                // Сохраняем технологии
                var techData = technologies.OrderByDescending(x => x.Value)
                                          .Take(15)
                                          .ToDictionary(x => x.Key, x => x.Value);
                
                File.WriteAllText("technologies.json", JsonConvert.SerializeObject(techData, Formatting.Indented));
                
                // Сохраняем типы занятости
                File.WriteAllText("employment_types.json", JsonConvert.SerializeObject(employmentTypes, Formatting.Indented));
                
                // Сохраняем зарплаты
                var salaryData = new Dictionary<string, object>();
                foreach (var level in salariesByExperience)
                {
                    if (level.Value.Count > 0)
                    {
                        salaryData[level.Key] = new
                        {
                            average = (int)level.Value.Average(),
                            min = level.Value.Min(),
                            max = level.Value.Max(),
                            count = level.Value.Count
                        };
                    }
                }
                
                File.WriteAllText("salaries.json", JsonConvert.SerializeObject(salaryData, Formatting.Indented));
                
                Console.WriteLine("\n✅ Результаты сохранены в JSON файлы:");
                Console.WriteLine("   • technologies.json");
                Console.WriteLine("   • employment_types.json");
                Console.WriteLine("   • salaries.json");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при сохранении результатов: {ex.Message}");
            }
        }
    }

    class Vacancy
    {
        public string Title { get; set; }
        public string Company { get; set; }
        public string Location { get; set; }
        public int SalaryFrom { get; set; }
        public int SalaryTo { get; set; }
        public string Experience { get; set; }
        public string EmploymentType { get; set; }
        public string Description { get; set; }
        public string Url { get; set; }
    }
}