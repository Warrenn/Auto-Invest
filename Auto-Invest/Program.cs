using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using IBApi;

namespace Auto_Invest
{
    public class Program
    {
        public static string GetName(Type t)
        {
            if (typeof(int) == t) return "int";
            if (typeof(long) == t) return "long";
            if (typeof(double) == t) return "double";
            if (typeof(string) == t) return "string";
            if (typeof(bool) == t) return "bool";
            if (typeof(char) == t) return "char";

            if (t.Namespace == "IBApi") return t.Name;
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                return $"KeyValuePair<{GetName(t.GetGenericArguments()[0])}, {GetName(t.GetGenericArguments()[1])}>";
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                return $"Dictionary<{GetName(t.GetGenericArguments()[0])}, {GetName(t.GetGenericArguments()[1])}>";
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(HashSet<>))
                return $"HashSet<{GetName(t.GetGenericArguments()[0])}>";
            return t.FullName;
        }
        public static void Main(string[] args)
        {
            var classT = typeof(EWrapper);

            foreach (var methodInfo in classT.GetMethods())
            {
                var parameters = methodInfo.GetParameters();

                if (parameters.Length < 2) continue;

                Console.WriteLine($"public class {methodInfo.Name.ToUpper()[0]}{methodInfo.Name.Substring(1)}Class {{");
                foreach (var parameterInfo in parameters)
                {
                    Console.WriteLine($"    public {GetName(parameterInfo.ParameterType)} {parameterInfo.Name.ToUpper()[0]}{parameterInfo.Name.Substring(1)} {{ get; set; }}");
                }
                Console.WriteLine($"}}\r\n");
            }

            foreach (var methodInfo in classT.GetMethods())
            {
                var methodName = $"{methodInfo.Name.ToUpper()[0]}{methodInfo.Name.Substring(1)}";

                var parameters = methodInfo.GetParameters();

                if (parameters.Length == 0)
                {
                    Console.WriteLine($"public event Action {methodName}Event;");
                    Console.WriteLine($"public void {methodInfo.Name}() => Post({methodName}Event);");
                    continue;
                }

                if (parameters.Length == 1)
                {
                    Console.WriteLine($"public event Action<{GetName(parameters[0].ParameterType)}> {methodName}Event;");
                    Console.WriteLine($"public void {methodInfo.Name}({GetName(parameters[0].ParameterType)} {parameters[0].Name}) => Post({methodName}Event, {parameters[0].Name});");
                    continue;
                }

                Console.WriteLine($"public event Action<{methodName}Class> {methodName}Event;");
                Console.WriteLine($"public void {methodInfo.Name}({string.Join(", ", parameters.Select(p => $"{GetName(p.ParameterType)} {p.Name}").ToArray())}) => Post({methodName}Event, new {methodName}Class {{{string.Join(",", parameters.Select(p => $"\r\n{p.Name.ToUpper()[0]}{p.Name.Substring(1)} = {p.Name}").ToArray())}\r\n}});");

                Console.WriteLine();
            }

        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
