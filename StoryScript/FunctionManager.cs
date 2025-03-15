using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MookStoryScript
{
    /// <summary>
    /// 标记可以被自动注册的脚本函数
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ScriptFuncAttribute : Attribute
    {
        public string Name { get; }

        public ScriptFuncAttribute(string name)
        {
            Name = name.ToLower();
        }
    }

    public class FunctionManager
    {
        private readonly Dictionary<string, Delegate> _functions;
        private readonly ExpressionManager _expressionManager;
        // 存储类型到其单例实例的映射
        private readonly Dictionary<Type, object> _singletonInstances = new Dictionary<Type, object>();
        // 存储已注册的对象
        private readonly Dictionary<string, object> _registeredObjects = new Dictionary<string, object>();

        private static DialogueManager _dialogueManager = null!;
        private static LocalizationManager _localizationManager = null!;

        public FunctionManager(ExpressionManager expressionManager, DialogueManager dialogueManager)
        {
            Console.WriteLine("Initializing FunctionManager...");
            _functions = new Dictionary<string, Delegate>();
            _expressionManager = expressionManager;
            _dialogueManager = dialogueManager;
            _localizationManager = dialogueManager.LocalizationManagers;

            // 自动注册所有程序集中的函数
            RegisterAllFunctions();
        }

        /// <summary>
        /// 注册所有程序集中标记了ScriptFunc的函数
        /// </summary>
        private void RegisterAllFunctions()
        {
            try
            {
                // 获取当前程序集
                var currentAssembly = typeof(FunctionManager).Assembly;
                var assemblies = new HashSet<Assembly>
                {
                    // 添加当前程序集
                    currentAssembly
                };

                // 获取所有引用了当前程序集的程序集
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        // 检查是否引用了当前程序集
                        if (assembly.GetReferencedAssemblies().Any(a => a.FullName == currentAssembly.FullName))
                        {
                            assemblies.Add(assembly);
                        }
                    }
                    catch (Exception)
                    {
                        // 忽略获取引用时的错误
                    }
                }

                // 扫描收集到的程序集
                foreach (var assembly in assemblies)
                {
                    try
                    {
                        // 获取程序集中的所有类型
                        var types = assembly.GetTypes();
                        foreach (var type in types)
                        {
                            try
                            {
                                RegisterFunctionsFromType(type);
                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLine($"Error registering functions from type {type.FullName}: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error scanning assembly {assembly.FullName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error registering functions: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取或创建类型的单例实例
        /// </summary>
        private object GetOrCreateSingleton(Type type)
        {
            if (!_singletonInstances.TryGetValue(type, out var instance))
            {
                instance = Activator.CreateInstance(type);
                if (instance == null)
                {
                    throw new InvalidOperationException($"Failed to create an instance of type {type.FullName}");
                }
                _singletonInstances[type] = instance;
            }
            return instance;
        }

        /// <summary>
        /// 从指定类型中注册所有标记了ScriptFunc特性的方法
        /// </summary>
        /// <param name="type">要注册函数的类型</param>
        /// <param name="instance">可选的实例对象，用于非静态方法</param>
        public void RegisterFunctionsFromType(Type type, object? instance = null)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttribute<ScriptFuncAttribute>() != null);

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<ScriptFuncAttribute>();
                if (attr == null) continue;

                // 获取方法的参数信息
                var parameters = method.GetParameters();
                Type delegateType;

                if (parameters.Length == 0)
                {
                    delegateType = System.Linq.Expressions.Expression.GetDelegateType(
                        new[] { method.ReturnType });
                }
                else
                {
                    // 创建适当的委托类型
                    var parameterTypes = parameters.Select(p => p.ParameterType).ToArray();
                    delegateType = System.Linq.Expressions.Expression.GetDelegateType(
                        parameterTypes.Concat(new[] { method.ReturnType }).ToArray());
                }

                // 创建委托并注册
                try
                {
                    Delegate action;
                    if (method.IsStatic)
                    {
                        // 静态方法
                        action = Delegate.CreateDelegate(delegateType, method);
                    }
                    else
                    {
                        // 对于实例方法，使用传入的实例或创建类型的实例
                        object? targetInstance = instance;
                        if (targetInstance == null)
                        {
                            targetInstance = GetOrCreateSingleton(type);
                        }
                        action = Delegate.CreateDelegate(delegateType, targetInstance, method);
                    }

                    RegisterFunc(attr.Name, action);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error registering function {method.Name}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 注册对象及其所有方法
        /// </summary>
        public void RegisterObject(string objectName, object instance)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                Console.Error.WriteLine("Object name cannot be empty");
                return;
            }

            objectName = objectName.ToLower();
            _registeredObjects[objectName] = instance;

            // 获取对象类型
            Type type = instance.GetType();

            // 注册所有公共方法
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                // 排除Object类的方法和属性访问器
                if (method.DeclaringType == typeof(object) ||
                    method.IsSpecialName) // 排除属性的getter/setter
                    continue;

                // 创建方法访问的函数名：objectName$methodName
                string funcName = $"{objectName}${method.Name}".ToLower();

                try
                {
                    // 获取方法的参数信息
                    var parameters = method.GetParameters();
                    Type delegateType;

                    if (parameters.Length == 0)
                    {
                        delegateType = System.Linq.Expressions.Expression.GetDelegateType(
                            new[] { method.ReturnType });
                    }
                    else
                    {
                        // 创建适当的委托类型
                        var parameterTypes = parameters.Select(p => p.ParameterType).ToArray();
                        delegateType = System.Linq.Expressions.Expression.GetDelegateType(
                            parameterTypes.Concat(new[] { method.ReturnType }).ToArray());
                    }

                    // 创建委托并注册
                    Delegate action = Delegate.CreateDelegate(delegateType, instance, method);
                    RegisterFunc(funcName, action);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error registering method {method.Name}: {ex.Message}");
                }
            }

            // 查找并注册标记了特性的方法
            RegisterFunctionsFromType(type, instance);
        }

        /// <summary>
        /// 获取已注册的对象
        /// </summary>
        public object GetObject(string objectName)
        {
            objectName = objectName.ToLower();
            if (_registeredObjects.TryGetValue(objectName, out var obj))
                return obj;

            throw new KeyNotFoundException($"Object not found: {objectName}");
        }

        /// <summary>
        /// 设置函数
        /// </summary>
        /// <param name="name">函数名称</param>
        /// <param name="function">函数</param>
        public void RegisterFunc(string name, Delegate function)
        {
            name = name.ToLower();
            _functions[name] = function;
            _expressionManager.SetFunction(name, function);
        }

        /// <summary>
        /// 添加函数（支持直接传递方法引用）
        /// </summary>
        /// <param name="name">函数名称</param>
        /// <param name="function">函数</param>
        public void AddFunc(string name, Delegate function)
        {
            RegisterFunc(name, function);
        }

        /// <summary>
        /// 获取函数
        /// </summary>
        /// <param name="name">函数名称</param>
        /// <returns>函数</returns>
        public Delegate? GetFunc(string name)
        {
            name = name.ToLower();
            if (_functions.TryGetValue(name, out var function))
            {
                return function;
            }
            return null;
        }

        /// <summary>
        /// 检查函数是否存在
        /// </summary>
        /// <param name="name">函数名称</param>
        /// <returns>是否存在</returns>
        public bool HasFunc(string name)
        {
            name = name.ToLower();
            return _functions.ContainsKey(name);
        }

        /// <summary>
        /// 获取所有函数名
        /// </summary>
        /// <returns>所有函数名</returns>
        public IEnumerable<string> GetAllFunctionNames()
        {
            return _functions.Keys;
        }


        // == 以下是自定义函数 ==

        /// <summary>
        /// 打印日志
        /// </summary>
        /// <param name="message">日志消息</param>
        [ScriptFunc("log")]
        public static void CsLog(object message)
        {
            Console.WriteLine(message);
        }

        /// <summary>
        /// 本地化函数，获取指定键的本地化文本
        /// </summary>
        /// <param name="key">本地化键</param>
        /// <returns>本地化文本</returns>
        [ScriptFunc("l")]
        public static string Localize(string key)
        {
            return _localizationManager.GetText(key);
        }

        /// <summary>
        /// 判断节点是否被访问过
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <returns>是否被访问过</returns>
        [ScriptFunc("visited")]
        public static bool Visited(string nodeName)
        {
            return Visited_Count(nodeName) > 0;
        }

        /// <summary>
        /// 获取节点访问次数
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <returns>访问次数</returns>
        [ScriptFunc("visited_count")]
        public static int Visited_Count(string nodeName)
        {
            if (string.IsNullOrEmpty(nodeName))
            {
                Console.Error.WriteLine("Node name cannot be empty");
                return 0;
            }

            if (!_dialogueManager.DialogueLoaders.DialogueNodes.ContainsKey(nodeName))
            {
                Console.Error.WriteLine($"Node not found: {nodeName}");
                return 0;
            }

            return _dialogueManager.DialogueProgresses.GetAllSectionsNodeVisitCount(nodeName);
        }

        /// <summary>
        /// 返回一个介于 0 和 1 之间的随机数
        /// </summary>
        /// <param name="digits">小数位数</param>
        /// <returns>随机浮点数</returns>
        [ScriptFunc("random")]
        public static float Random_Float(int digits = 2)
        {
            return (float)Math.Round(new Random().NextDouble(), digits);
        }

        /// <summary>
        /// 返回一个介于 min 和 max 之间的随机数
        /// </summary>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="digits">小数位数</param>
        /// <returns>随机浮点数</returns>
        [ScriptFunc("random_range")]
        public static float Random_Float_Range(float min, float max, int digits = 2)
        {
            return (float)Math.Round(new Random().NextDouble() * (max - min) + min, digits);
        }

        /// <summary>
        /// 介于 1 和 sides 之间（含 1 和 sides ）的随机整数
        /// </summary>
        /// <param name="sides">骰子面数</param>
        /// <returns>随机整数</returns>
        [ScriptFunc("dice")]
        public static int Random_Dice(int sides)
        {
            return new Random().Next(1, sides + 1);
        }

    }
}
