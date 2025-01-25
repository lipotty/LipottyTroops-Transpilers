using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using System.Reflection.Emit;

namespace LipottyTroops
{
    [HarmonyPatch(typeof(MobileParty), "RecruitVolunteersFromNotable")]
    public class RecruitVolunteersPatch
    {
        // 使用 Harmony 的 Transpiler 修改原方法
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                // 找到 GetRecruitVolunteerFromIndividual 的调用
                if (codes[i].opcode == OpCodes.Call && codes[i].operand.ToString().Contains("GetRecruitVolunteerFromIndividual"))
                {
                    // 在调用之前插入兵种集合检查逻辑
                    codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_0)); // 加载 mobileParty
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldloc_S, 4)); // 加载 characterObject
                    codes.Insert(i + 2, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RecruitVolunteersPatch), "CheckTroopCollectionLimit")));
                    codes.Insert(i + 3, new CodeInstruction(OpCodes.Brfalse, codes[i + 4].operand)); // 如果检查失败，跳过招募
                    i += 4; // 调整索引
                }

                // 找到遍历6个兵种的循环
                if (codes[i].opcode == OpCodes.Blt && codes[i].operand.ToString().Contains("i < 6"))
                {
                    // 在循环开始前插入兵种集合检查逻辑
                    codes.Insert(i, new CodeInstruction(OpCodes.Ldloc_S, 4)); // 加载 characterObject
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RecruitVolunteersPatch), "ShouldSkipTroop")));
                    codes.Insert(i + 2, new CodeInstruction(OpCodes.Brtrue, codes[i + 3].operand)); // 如果 ShouldSkipTroop 返回 true，则跳过当前兵种
                    i += 3; // 调整索引
                }
            }

            return codes;
        }

        // 检查兵种是否需要跳过
        private static bool ShouldSkipTroop(CharacterObject characterObject)
        {
            try
            {
                // 获取 SoldierLimitBehavior
                var soldierLimitBehavior = Campaign.Current.GetCampaignBehavior<SoldierLimitBehavior>();
                if (soldierLimitBehavior == null)
                {
                    Debug.Print("SoldierLimitBehavior not found!");
                    return false; // 如果未找到 SoldierLimitBehavior，则不跳过
                }

                // 判断兵种是否属于某个集合
                var troopLimit = soldierLimitBehavior.GetTroopLimitForTroop(characterObject.StringId);
                if (troopLimit == null)
                {
                    Debug.Print($"Troop {characterObject.Name} does not belong to any collection!");
                    return false; // 如果兵种不属于任何集合，则不跳过
                }

                // 检查兵种集合是否达到上限
                if (soldierLimitBehavior.IsTroopLimitExceeded(troopLimit))
                {
                    Debug.Print($"Troop limit exceeded for {characterObject.Name} in collection {troopLimit.Name}!");
                    return true; // 如果达到上限，则跳过
                }

                return false; // 如果未达到上限，则不跳过
            }
            catch (Exception ex)
            {
                Debug.Print($"Exception in ShouldSkipTroop: {ex}");
                return false; // 如果发生异常，则不跳过
            }
        }
    }
}
