using EpinelPS.Data;

namespace EpinelPS.LobbyServer.Messenger;

[GameRequest("/messenger/get")]
public class GetMessages : LobbyMessage
{
    protected override async Task HandleAsync()
    {
        ReqGetMessages req = await ReadData<ReqGetMessages>();
        User user = GetUser();

        CheckAndCreateAvailableMessages(user);
        CheckAndEnrollSubQuests(user);

        ResGetMessages response = new();

        IEnumerable<NetMessage> newMessages = user.MessengerData.Where(x => x.Seq >= req.Seq);

        foreach (NetMessage? item in newMessages)
        {
            response.Messages.Add(item);
        }

        await WriteDataAsync(response);
    }

    private void CheckAndCreateAvailableMessages(User user)
    {
        foreach (KeyValuePair<int, MessengerConditionTriggerRecord> messageCondition in GameData.Instance.MessageConditions)
        {
            int conditionId = messageCondition.Key;
            MessengerConditionTriggerRecord msgCondition = messageCondition.Value;

            if (IsTriggerListSatisfied(user, msgCondition.TriggerList))
            {
                bool messageExists = user.MessengerData.Any(m => m.ConversationId == msgCondition.Tid);
                if (!messageExists)
                {
                    KeyValuePair<string, MessengerDialogRecord> conversation = GameData.Instance.Messages.FirstOrDefault(x =>
                        x.Value.ConversationId == msgCondition.Tid && x.Value.IsOpener);

                    if (conversation.Value != null)
                    {
                        user.CreateMessage(conversation.Value);
                    }
                }
            }
        }
    }

    private void CheckAndEnrollSubQuests(User user)
    {
        foreach (KeyValuePair<int, SubQuestRecord> subQuestKv in GameData.Instance.Subquests)
        {
            SubQuestRecord subQuest = subQuestKv.Value;

            // Check prerequisite subquest
            if (subQuest.BeforeSubQuestId > 0)
            {
                if (!user.SubQuestData.TryGetValue(subQuest.BeforeSubQuestId, out bool prevCompleted) || !prevCompleted)
                    continue;
            }

            // Check trigger conditions
            if (!IsTriggerListSatisfied(user, subQuest.TriggerList))
                continue;

            // Auto-enroll if not already enrolled
            if (!user.SubQuestData.ContainsKey(subQuest.Id))
            {
                user.SetSubQuest(subQuest.Id, false);
            }
        }
    }

    private bool IsTriggerListSatisfied(User user, List<TriggerData> triggerList)
    {
        if (triggerList == null)
            return true;

        foreach (TriggerData trigger in triggerList)
        {
            if (trigger.Trigger == Data.Trigger.None || trigger.ConditionId == 0)
                continue;

            if (!CheckTriggerCondition(user, trigger))
            {
                return false; // All conditions must be satisfied
            }
        }

        return true;
    }

    private bool CheckTriggerCondition(User user, TriggerData trigger)
    {
        return GameContext.Triggers.Any(t =>
            t.UserId == user.ID &&
            t.Type == trigger.Trigger &&
            t.ConditionId == trigger.ConditionId &&
            t.Value >= trigger.ConditionValue);
    }
}
