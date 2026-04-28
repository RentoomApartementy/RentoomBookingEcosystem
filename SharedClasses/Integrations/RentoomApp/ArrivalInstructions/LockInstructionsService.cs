namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.ArrivalInstructions
{
    public class LockInstructionsService
    {
        private static readonly Dictionary<string, LockInstructionsDTO> _instructionsByLanguage = new(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = new LockInstructionsDTO
            {
                CylinderOpen =
                    "## How to open - Spinning lock\n" +
                    "1. Enter the code or unlock remotely.\n" +
                    "(Each keystroke will be confirmed by a beep)\n" +
                    "2. When the light turns blue, turn the knob\n" +
                    "(The lock is on the right-hand side of the door, turn it to the left)\n" +
                    "(Lock on the left-hand side of the door, turn to the right)\n" +
                    "until you hear the lock retract.\n" +
                    "3. Open the door",

                CylinderClose =
                    "## How to close - Spinning lock\n" +
                    "1. Enter the code\n" +
                    "(Each keystroke will be confirmed by a beep)\n" +
                    "2. When the light turns blue, turn the knob in the opposite direction\n" +
                    "(The lock is on the right-hand side of the door, turn it to the right)\n" +
                    "(Lock on the left-hand side of the door, turn to the left)\n" +
                    "until you hear the lock engage.\n" +
                    "3. The door is locked.",

                PanelOpen =
                    "## How to open - Panel lock\n" +
                    "1. Light up the keyboard by pressing any button on the panel or open it remotely.\n" +
                    "2. Enter your code\n" +
                    "3. If it lights up blue, open the door.",

                PanelClose =
                    "## How to close - Panel lock\n" +
                    "Just close the door behind you."
            },

            ["pl"] = new LockInstructionsDTO
            {
                CylinderOpen =
                    "## Jak otworzyć - Zamek obrotowy\n" +
                    "1. Wpisz kod lub otwórz zdalnie\n" +
                    "(Każde kliknięcie przycisku na klawiaturze będzie potwierdzone dźwiękiem)\n" +
                    "2. Jeżeli zaświeci się na niebiesko, przekręcaj gałką\n" +
                    "(Zamek po prawej stronie drzwi, kręć w lewo)\n" +
                    "(Zamek po lewej stronie drzwi, kręć w prawo)\n" +
                    "do momentu aż usłyszysz cofający się zamek.\n" +
                    "3. Otwórz drzwi",

                CylinderClose =
                    "## Jak zamknąć - Zamek obrotowy\n" +
                    "1. Wpisz kod\n" +
                    "(Każde kliknięcie przycisku na klawiaturze będzie potwierdzone dźwiękiem)\n" +
                    "2. Jeżeli zaświeci się na niebiesko, przekręcaj gałką w przeciwnym kierunku\n" +
                    "(Zamek po prawej stronie drzwi, kręć w prawo)\n" +
                    "(Zamek po lewej stronie drzwi, kręć w lewo)\n" +
                    "do momentu aż usłyszysz zamykający się zamek.\n" +
                    "3. Drzwi są zamknięte.",

                PanelOpen =
                    "## Jak otworzyć - Zamek panelowy\n" +
                    "1. Podświetl klawiaturę wciskając dowolny przycisk na panelu lub otwórz zdalnie.\n" +
                    "2. Wpisz swój kod\n" +
                    "3. Jeżeli zaświeci się na niebiesko otwórz drzwi.",

                PanelClose =
                    "## Jak zamknąć - Zamek panelowy\n" +
                    "Wystarczy zamknąć za sobą drzwi."
            }
        };

        public LockInstructionsDTO GetLockInstructions(string? language)
        {
            var normalized = NormalizeLanguage(language);
            return _instructionsByLanguage.TryGetValue(normalized, out var instructions)
                ? instructions
                : _instructionsByLanguage["en"];
        }

        private static string NormalizeLanguage(string? language)
        {
            if (string.IsNullOrWhiteSpace(language))
                return "en";

            var trimmed = language.Trim().ToLowerInvariant().Replace('_', '-');
            var dashIndex = trimmed.IndexOf('-', StringComparison.Ordinal);
            if (dashIndex > 0)
                trimmed = trimmed[..dashIndex];

            return trimmed switch
            {
                "pl" or "pol" => "pl",
                "en" or "eng" => "en",
                _ => "en"
            };
        }
    }
}
