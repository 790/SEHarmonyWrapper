using System;
using SEHarmonyWrapper;
using Harmony;

namespace Example_Plugin
{
    public class Example : ModBase
    {
        public Logger logger;
        public void Main(HarmonyInstance harmony, Logger logger)
        {
            this.logger = logger;
            logger.Log("Hello from plubgin");
        }
        public void Init(object gameObject)
        {
            
        }
        public void Update()
        {
            
        }
        public void Dispose()
        {
            
        }
    }
}
