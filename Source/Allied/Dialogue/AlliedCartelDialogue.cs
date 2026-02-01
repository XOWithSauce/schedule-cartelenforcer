
using static CartelEnforcer.CartelPersuade;

namespace CartelEnforcer
{
    public static class AlliedCartelDialogue
    {

        public static readonly List<string> dealerEntryNodeTexts = new List<string>()
        {
            "You've got a lot of nerve showing up like this.",
            "What's your problem?",
            "What do you want?!",
            "Start talking or start walking young punk.",
        };

        public static readonly List<string> alliedDialogueKeys = new()
        {
            "CLOTHING_SIMILARITY", "CARTEL_INFLUENCE", "THREATEN_CARTEL", "SPREAD_RUMOURS", "EXIT"
        };
        // Key ChoiceLabel, Value ChoiceText template
        public static readonly Dictionary<string, List<string>> alliedDialogue = new()
        {
            {alliedDialogueKeys[0], new() {
                "Check out my new drip.",
                "Same style, same goals, yeah?",
                "You're rocking the same vest?"
            }},

            {alliedDialogueKeys[1], new() {
                "The Benzies are small time!",
                "Are the Benzies having it rough lately?",
                "The Benzies are a bunch of young punks."
            }},

            {alliedDialogueKeys[2], new() {
                "Check the piece. You want a taste of lead?",
                "I'm packing some serious heat.",
                "You're about to get yo' ass whooped."
            }},

            {alliedDialogueKeys[3], new() {
                "You know that Thomas has been talking to the cops right?",
                "I heard that Thomas is going to cut you out.",
                "Better watch out, that Thomas is a snake."
            }},

            {alliedDialogueKeys[4], new() {
                "Nevermind.",
                "I got to go.",
            }}
        };

        // Key ChoiceLabel, Value float 0.0-1.0 chances stored that get calculated from base + modifiers
        public static Dictionary<string, float> persuasionChances = new()
        {
            {alliedDialogueKeys[0], 0.0f},
            {alliedDialogueKeys[1], 0.0f},
            {alliedDialogueKeys[2], 0.0f},
            {alliedDialogueKeys[3], 0.0f}
        };

        public static readonly Dictionary<string, float> persuasionBaseChances = new()
        {
            {alliedDialogueKeys[0], CLOTHING_SIMILARITY_BASE_CHANCE},
            {alliedDialogueKeys[1], CARTEL_INFLUENCE_BASE_CHANCE},
            {alliedDialogueKeys[2], THREATEN_CARTEL_BASE_CHANCE},
            {alliedDialogueKeys[3], SPREAD_RUMOURS_BASE_CHANCE}
        };
    }


}