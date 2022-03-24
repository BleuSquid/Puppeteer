using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TwitchToolkit.Commands.ViewerCommands;

namespace ΩPuppeteer.HarmonyPatches
{
    /// <summary>
    ///     A Harmony patch class.
    /// </summary>
    /// <remarks>
    ///     The code within the class requires a call to
    ///     <see cref="Harmony.PatchAll(Assembly)"/> to be called somewhere
    ///     else.
    /// </remarks>
    [HarmonyPatch]
    public static class ModSettingsPatch
    {
        private static MethodInfo _usernameProperty;
        private static MethodInfo _replacePlaceholder;

        /// <summary>
        ///     Called twice before patching a method specified in
        ///     <see cref="TargetMethods"/>. Once with no
        ///     <see cref="MethodBase"/> argument, and another with a
        ///     <see cref="MethodBase"/> argument.
        /// </summary>
        /// <returns>Whether or not the patch should be done</returns>
        /// <remarks>
        ///     More information about Prepare can be found at
        ///     https://harmony.pardeike.net/articles/patching-auxilary.html#prepare
        /// </remarks>
        public static bool Prepare()
        {
            // Asks Harmony to grab the property getter to be used later.
            _usernameProperty = AccessTools.PropertyGetter(AccessTools.TypeByName("TwitchLib.Client.Models.Interfaces.ITwitchMessage"), "Username");

            // Asks Harmony to grab the method TwitchToolkit.Helper:ReplacePlaceholder to be used later
            _replacePlaceholder = AccessTools.Method("TwitchToolkit.Helper:ReplacePlaceholder");

            return true;
        }

        /// <summary>
        ///     Returns an enumerable of methods to perform the code within
        ///     <see cref="Transpiler"/> on.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(ModSettings), "RunCommand");
        }

        /// <summary>
        ///     Changes the compiler generated IL code for the methods returned
        ///     from <see cref="TargetMethods"/>.
        /// </summary>
        /// <param name="instructions">The IL code for a given method</param>
        /// <returns>The modified IL code for the given method</returns>
        /// <remarks>
        ///     This method modifies Twitch Toolkit's ModSettings command to
        ///     prefix its response with the user's username.
        /// </remarks>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            //  We'll load the username of the viewer first.
            var transpiler = new List<CodeInstruction>
            {
                // Loads the first argument of the method. In this case it'd be the ITwitchMessage argument.
                new CodeInstruction(OpCodes.Ldarg_1),

                // Since the previous statement loaded the ITwitchMessage instance that was passed
                // to the command, it'll call the Username property getter to get the value of it.
                // This would, for example, be "sirrandoo" or "saschahi".
                new CodeInstruction(OpCodes.Callvirt, _usernameProperty)
            };

            var nop = false;
            // Next we'll go through every unmodified piece of IL code to look for the "ReplacePlaceholder" call
            // ModSettings does.
            foreach (CodeInstruction instruction in instructions)
            {
                // Once we've injected the FormatMessage call, we don't need
                // everything else at the end of the method, so we'll make
                // the interpreter ignore them by changing the opcode to
                // Nop.
                if (nop && instruction.opcode != OpCodes.Ret)
                {
                    instruction.opcode = OpCodes.Nop;
                }

                // We'll always add the original IL code as we don't need to remove anything specific for
                // this kind of patch.
                transpiler.Add(instruction);

                // If we aren't at the point where the method is called, we'll just keep adding the original IL code.
                if (instruction.opcode != OpCodes.Call || !instruction.OperandIs(_replacePlaceholder))
                {
                    continue;
                }

                // Now that we've found the call, we'll then tell it to call the FormatMessage method
                // within this patch class so that we can prepend @username to the message.
                transpiler.Add(CodeInstruction.Call(typeof(ModSettingsPatch), "FormatMessage"));

                // Now that @username is in the message, we'll send it to Twitch chat.
                transpiler.Add(CodeInstruction.Call("ToolkitCore.TwitchWrapper:SendChatMessage"));

                // Now that the injection is done, we can tell the interpreter to ignore everything else.
                nop = true;
            }

            // Finally, we'll return all the modified IL code for the method.
            return transpiler;
        }

        /// <summary>
        ///     Prepends <see cref="username"/> to <see cref="message"/>.
        /// </summary>
        /// <param name="username">The username to prepend</param>
        /// <param name="message">The message to append</param>
        /// <returns>The modified message</returns>
        private static string FormatMessage(string username, string message) => $"@{username} {message}";
    }
}
