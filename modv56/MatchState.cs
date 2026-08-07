using System.Collections.Generic;

namespace LCRanked
{
    public class Ruleset
    {
        public string moon;
        public int startingCredits;
        public int seed;
        public bool lockSettingsMidRun;
        public string scoringMode;
        public string weatherMS;
    }

    public class ParticipantInfo
    {
        public string playerId;
        public string playerName;
    }


    public class MatchState
    {
        public string matchId;
        public string mode;
        public Ruleset ruleset;
        public string rulesetJson;
        public string weatherMS;
        public List<ParticipantInfo> participants = new List<ParticipantInfo>();
        public long startTimestampMs;

        public bool runFinished;
        public int collectedValue;
        public bool survived;
        public bool aliveAt2pm;
        public bool aliveAt9pm;

        public bool HasActiveMatch => matchId != null;

        public void Reset()
        {
            matchId = null;
            mode = null;
            ruleset = null;
            rulesetJson = null;
            participants.Clear();
            startTimestampMs = 0;
            runFinished = false;
            collectedValue = 0;
            survived = false;
            aliveAt2pm = false;
            aliveAt9pm = false;

            

        }
    }
}
