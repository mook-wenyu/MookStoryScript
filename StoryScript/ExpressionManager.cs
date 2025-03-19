using System;
using DynamicExpresso;
using StoryScript;

namespace MookStoryScript
{
    public class ExpressionManager
    {
        private readonly Interpreter _interpreter = new Interpreter(InterpreterOptions.DefaultCaseInsensitive)
            .EnableReflection();

        public ExpressionManager()
        {
            Logger.Log("Initializing ExpressionManager...");
        }

        /// <summary>
        /// 设置变量到解释器
        /// </summary>
        public void SetVariable(string name, object value)
        {
            _interpreter.SetVariable(name, value);
        }

        /// <summary>
        /// 设置函数到解释器
        /// </summary>
        /// <param name="name">函数名称</param>
        /// <param name="function">函数</param>
        public void SetFunction(string name, Delegate function)
        {
            _interpreter.SetFunction(name, function);
        }

        /// <summary>
        /// 获取解释器
        /// </summary>
        /// <returns>解释器</returns>
        public Interpreter GetInterpreter()
        {
            return _interpreter;
        }

        /// <summary>
        /// 计算表达式的值
        /// </summary>
        /// <param name="expression">要计算的表达式</param>
        /// <returns>表达式的计算结果</returns>
        public object? Evaluate(string expression)
        {
            try
            {
                return _interpreter.Eval(expression);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Expression parsing error: {expression}\n{ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 计算表达式的值
        /// </summary>
        /// <typeparam name="T">表达式返回的类型</typeparam>
        /// <param name="expression">要计算的表达式</param>
        /// <returns>表达式的计算结果</returns>
        public T? Evaluate<T>(string expression)
        {
            try
            {
                return _interpreter.Eval<T>(expression);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Expression parsing error: {expression}\n{ex.Message}");
                return default(T?);
            }
        }

        /// <summary>
        /// 计算条件表达式的值
        /// </summary>
        /// <param name="condition">要计算的条件表达式</param>
        /// <returns>条件表达式的计算结果</returns>
        public bool EvaluateCondition(string condition)
        {
            if (string.IsNullOrEmpty(condition))
            {
                return true; // 空条件默认为真
            }

            try
            {
                bool result = Evaluate<bool>(condition);
                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Condition expression calculation error: {condition}\n{ex.Message}");
                return false; // 出错时返回 false
            }
        }

    }
}
