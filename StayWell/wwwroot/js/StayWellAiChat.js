window.staywellAiChat = window.staywellAiChat || {};

window.staywellAiChat.scrollToBottom = function (container) {
    if (!container) return;
    container.scrollTop = container.scrollHeight;
};

window.staywellAiChat.isSpeechSupported = function () {
    return !!(window.SpeechRecognition || window.webkitSpeechRecognition);
};

window.staywellAiChat.startDictation = function (dotNetRef, language) {
    var SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!SpeechRecognition) {
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('OnSpeechSupportChanged', false);
            dotNetRef.invokeMethodAsync('OnSpeechError', 'Ta przeglądarka nie obsługuje rozpoznawania mowy.');
        }
        return;
    }

    if (window.staywellAiChat._recognition) {
        try {
            window.staywellAiChat._recognition.stop();
        } catch (_) {
        }
        window.staywellAiChat._recognition = null;
    }

    var recognition = new SpeechRecognition();
    recognition.lang = language || 'pl-PL';
    recognition.continuous = true;
    recognition.interimResults = true;
    recognition.maxAlternatives = 1;

    window.staywellAiChat._dotNetRef = dotNetRef;
    window.staywellAiChat._recognition = recognition;
    window.staywellAiChat._finalTranscript = '';

    recognition.onstart = function () {
        if (window.staywellAiChat._dotNetRef) {
            window.staywellAiChat._dotNetRef.invokeMethodAsync('OnSpeechStateChanged', true);
        }
    };

    recognition.onresult = function (event) {
        var finalTranscript = window.staywellAiChat._finalTranscript || '';
        var interimTranscript = '';

        for (var i = event.resultIndex; i < event.results.length; i++) {
            var segment = event.results[i][0] && event.results[i][0].transcript
                ? event.results[i][0].transcript
                : '';
            if (!segment) {
                continue;
            }

            if (event.results[i].isFinal) {
                finalTranscript += segment + ' ';
            } else {
                interimTranscript += segment + ' ';
            }
        }

        window.staywellAiChat._finalTranscript = finalTranscript;
        var transcript = (finalTranscript + interimTranscript).trim();

        if (window.staywellAiChat._dotNetRef) {
            window.staywellAiChat._dotNetRef.invokeMethodAsync('OnSpeechTranscriptChanged', transcript);
        }
    };

    recognition.onerror = function (event) {
        if (window.staywellAiChat._dotNetRef) {
            var message = event && event.error ? ('Błąd mikrofonu: ' + event.error) : 'Nie udało się uruchomić mikrofonu.';
            window.staywellAiChat._dotNetRef.invokeMethodAsync('OnSpeechError', message);
        }
    };

    recognition.onend = function () {
        window.staywellAiChat._recognition = null;
        window.staywellAiChat._finalTranscript = '';
        if (window.staywellAiChat._dotNetRef) {
            window.staywellAiChat._dotNetRef.invokeMethodAsync('OnSpeechStateChanged', false);
        }
    };

    try {
        recognition.start();
    } catch (_) {
        if (window.staywellAiChat._dotNetRef) {
            window.staywellAiChat._dotNetRef.invokeMethodAsync('OnSpeechError', 'Nie można uruchomić rozpoznawania mowy.');
        }
    }
};

window.staywellAiChat.stopDictation = function () {
    if (!window.staywellAiChat._recognition) return;

    var recognition = window.staywellAiChat._recognition;
    window.staywellAiChat._recognition = null;
    window.staywellAiChat._finalTranscript = '';

    try {
        recognition.stop();
    } catch (_) {
    }
};
