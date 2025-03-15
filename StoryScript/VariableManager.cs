using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using Newtonsoft.Json;

namespace MookStoryScript
{
    /// <summary>
    /// 标记可以被自动注册的脚本变量
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class ScriptVarAttribute : Attribute
    {
        public string Name { get; }

        public ScriptVarAttribute(string name)
        {
            Name = name.ToLower();
        }
    }

    public class VariableManager
    {
        // 存储游戏变量的委托
        private readonly Dictionary<string, (Func<object> getter, Action<object> setter)> _builtinVariables;
        private readonly Dictionary<string, object> _variables;
        private readonly ExpressionManager _expressionManager;
        // 存储类型到其单例实例的映射
        private readonly Dictionary<Type, object> _singletonInstances = new Dictionary<Type, object>();
        // 存储已注册的对象
        private readonly Dictionary<string, object> _registeredObjects = new Dictionary<string, object>();

        public int TestMoney { get; set; } = 1;

        /// <summary>
        /// 初始化变量系统
        /// </summary>
        public VariableManager(ExpressionManager expressionManager)
        {
            Console.WriteLine("Initializing VariableManager...");
            _builtinVariables = new Dictionary<string, (Func<object> getter, Action<object> setter)>();
            _variables = new Dictionary<string, object>();
            _expressionManager = expressionManager;

            // 自动注册所有程序集中的变量
            RegisterAllVariables();
        }

        /// <summary>
        /// 注册所有程序集中标记了ScriptVar的属性和字段
        /// </summary>
        private void RegisterAllVariables()
        {
            try
            {
                // 获取当前程序集
                var currentAssembly = typeof(VariableManager).Assembly;
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
                        Console.WriteLine($"Scanning assembly variables: {assembly.FullName}");

                        // 获取程序集中的所有类型
                        var types = assembly.GetTypes();
                        foreach (var type in types)
                        {
                            try
                            {
                                RegisterVariablesFromType(type);
                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLine($"Error registering variables from type {type.FullName}: {ex.Message}");
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
                Console.Error.WriteLine($"Error registering variables: {ex.Message}");
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
                    throw new InvalidOperationException($"Failed to create instance of type {type.FullName}");
                }
                _singletonInstances[type] = instance;
            }
            return instance;
        }

        /// <summary>
        /// 从指定类型中注册所有标记了ScriptVar特性的属性和字段
        /// </summary>
        public void RegisterVariablesFromType(Type type, object? instance = null)
        {
            // 获取所有标记了ScriptVar特性的属性
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<ScriptVarAttribute>() != null);

            foreach (var property in properties)
            {
                var attr = property.GetCustomAttribute<ScriptVarAttribute>();
                if (attr == null) continue;

                try
                {
                    if (property.GetMethod.IsStatic)
                    {
                        // 静态属性处理
                        Func<object> getter = () => property.GetValue(null);
                        Action<object> setter = (value) => property.SetValue(null, value);

                        RegisterBuiltinVariable(attr.Name, getter, setter);
                        Console.WriteLine($"Registered static variable: {attr.Name} [{type.FullName}.{property.Name}]");
                    }
                    else
                    {
                        // 对于实例属性，使用传入的实例或创建新实例
                        object? targetInstance = instance;
                        if (targetInstance == null)
                        {
                            targetInstance = GetOrCreateSingleton(type);
                        }

                        // 创建getter和setter委托
                        Func<object> getter = () => property.GetValue(targetInstance);
                        Action<object> setter = (value) => property.SetValue(targetInstance, value);

                        RegisterBuiltinVariable(attr.Name, getter, setter);
                        Console.WriteLine($"Registered instance variable: {attr.Name} [{type.FullName}.{property.Name}]");
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error registering variable {property.Name}: {ex.Message}");
                }
            }

            // 处理字段
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                .Where(f => f.GetCustomAttribute<ScriptVarAttribute>() != null);

            foreach (var field in fields)
            {
                var attr = field.GetCustomAttribute<ScriptVarAttribute>();
                if (attr == null) continue;

                try
                {
                    if (field.IsStatic)
                    {
                        // 静态字段处理
                        Func<object> getter = () => field.GetValue(null);
                        Action<object> setter = (value) => field.SetValue(null, value);

                        RegisterBuiltinVariable(attr.Name, getter, setter);
                        Console.WriteLine($"Registered static field: {attr.Name} [{type.FullName}.{field.Name}]");
                    }
                    else
                    {
                        // 对于实例字段，使用传入的实例或创建新实例
                        object? targetInstance = instance;
                        if (targetInstance == null)
                        {
                            targetInstance = GetOrCreateSingleton(type);
                        }

                        // 创建getter和setter委托
                        Func<object> getter = () => field.GetValue(targetInstance);
                        Action<object> setter = (value) => field.SetValue(targetInstance, value);

                        RegisterBuiltinVariable(attr.Name, getter, setter);
                        Console.WriteLine($"Registered instance field: {attr.Name} [{type.FullName}.{field.Name}]");
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error registering field {field.Name}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 注册对象及其所有成员
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

            // 注册所有属性
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                // 创建属性访问的变量名：objectName$propertyName
                string varName = $"{objectName}${property.Name}".ToLower();

                // 创建getter和setter
                Func<object> getter = () => property.GetValue(instance);
                Action<object> setter = (value) => property.SetValue(instance, value);

                // 注册到变量管理器
                RegisterBuiltinVariable(varName, getter, setter);
                Console.WriteLine($"Registered object property: {varName}");
            }

            // 注册所有字段
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                // 创建字段访问的变量名：objectName$fieldName
                string varName = $"{objectName}${field.Name}".ToLower();

                // 创建getter和setter
                Func<object> getter = () => field.GetValue(instance);
                Action<object> setter = (value) => field.SetValue(instance, value);

                // 注册到变量管理器
                RegisterBuiltinVariable(varName, getter, setter);
                Console.WriteLine($"Registered object field: {varName}");
            }

            // 查找并注册标记了特性的成员
            RegisterVariablesFromType(type, instance);

            Console.WriteLine($"Registered object: {objectName} [{type.FullName}]");
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
        /// 注册内置变量
        /// </summary>
        public void RegisterBuiltinVariable(string name, Func<object> getter, Action<object> setter)
        {
            name = name.ToLower();
            if (string.IsNullOrEmpty(name))
            {
                Console.Error.WriteLine("Variable name cannot be empty");
                return;
            }

            _builtinVariables[name] = (getter, setter);
            // 同步到表达式计算器
            _expressionManager.SetVariable(name, getter());
        }

        public void RegisterBuiltinObject(string name, object instance)
        {
            name = name.ToLower();
            _registeredObjects[name] = instance;
            _expressionManager.SetVariable(name, instance);
        }

        /// <summary>
        /// 保存脚本变量
        /// </summary>
        public Dictionary<string, object> SaveVariables()
        {
            // 只保存非内置变量（即故事脚本中设置的变量）
            return _variables;
        }

        /// <summary>
        /// 加载脚本变量
        /// </summary>
        public void LoadVariables(Dictionary<string, object> variables)
        {
            foreach (var pair in variables)
            {
                // 只加载非内置变量（即故事脚本中设置的变量）
                if (!_builtinVariables.ContainsKey(pair.Key))
                {
                    _variables[pair.Key] = pair.Value;
                    _expressionManager.SetVariable(pair.Key, pair.Value);
                }
            }
        }

        /// <summary>
        /// 设置变量值
        /// </summary>
        public void Set(string name, object value)
        {
            name = name.ToLower();
            try
            {
                if (_builtinVariables.TryGetValue(name, out var variable))
                {
                    // 如果是内置变量，调用其setter
                    variable.setter(value);
                    // 同步到表达式计算器
                    _expressionManager.SetVariable(name, value);
                    return;
                }

                // 如果是非内置变量（故事脚本变量），使用_variables保存
                _variables[name] = value;
                // 同步到表达式计算器
                _expressionManager.SetVariable(name, value);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to set variable {name}: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取变量值
        /// </summary>
        public T? Get<T>(string name, T? defaultValue = default)
        {
            name = name.ToLower();
            try
            {
                if (_builtinVariables.TryGetValue(name, out var variable))
                {
                    // 如果是内置变量，使用其getter
                    return (T)Convert.ChangeType(variable.getter(), typeof(T));
                }

                // 如果不是内置变量，从_variables中获取
                if (_variables.TryGetValue(name, out object? value))
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }

                return defaultValue;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to get variable {name}: {ex.Message}");
                return defaultValue;
            }
        }

        /// <summary>
        /// 检查变量是否存在
        /// </summary>
        public bool Exists(string name)
        {
            name = name.ToLower();
            return _builtinVariables.ContainsKey(name) ||
                   _variables.ContainsKey(name);
        }

        /// <summary>
        /// 获取所有变量名
        /// </summary>
        public IEnumerable<string> GetAllVariableNames()
        {
            var names = new HashSet<string>();
            foreach (string name in _builtinVariables.Keys)
            {
                names.Add(name);
            }
            foreach (string name in _variables.Keys)
            {
                names.Add(name);
            }
            return names;
        }

        /// <summary>
        /// 获取所有内置变量名
        /// </summary>
        public IEnumerable<string> GetBuiltInVariableNames()
        {
            return _builtinVariables.Keys;
        }

        /// <summary>
        /// 获取脚本变量名
        /// </summary>
        public IEnumerable<string> GetVariableNames()
        {
            return _variables.Keys;
        }

    }
}
