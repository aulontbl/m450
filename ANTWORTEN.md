# Antworten – Lernaufgabe Testen mit Test Doubles

Gewähltes Testframework: **xUnit** (`tests/TestDoublesExercise.Tests`).

```bash
dotnet test tests/TestDoublesExercise.Tests/TestDoublesExercise.Tests.csproj
```

## Aufgabe 1 – Nur die Schnittstelle ist bekannt

`ReservationService` hängt nur von der Schnittstelle `ISeatAvailability` ab, nicht von einer konkreten Klasse.
Deshalb können wir im Testprojekt eine eigene Implementierung (`SeatAvailabilityStub`) schreiben und dem
Service im Konstruktor übergeben. Der Service merkt keinen Unterschied.

Was die Schnittstelle bereits festlegt: Es gibt eine Methode `GetAvailableSeats`, die einen Veranstaltungscode
(`string`) entgegennimmt und eine Anzahl (`int`) zurückgibt. Mehr sagt sie nicht – z. B. nicht, was bei einem
unbekannten Code passiert oder ob negative Werte möglich sind.

Fachliche Annahme: `GetAvailableSeats(eventCode)` liefert die Anzahl **aktuell noch freier** Plätze der
Veranstaltung (≥ 0), unbekannte Veranstaltungen haben 0 freie Plätze. Eine Reservierung ist möglich, wenn
`verfügbar >= angefragt` ist. Der Stub berechnet nichts, sondern gibt pro Code einen fest eingestellten Wert zurück.

## Aufgabe 2 – Fehlende Aufrufe sichtbar machen

Ohne Produktionscode zu ändern, zeichnen die Test Doubles selbst auf, ob sie benutzt wurden:
Der Stub merkt sich die abgefragten Codes (`RequestedEventCodes`), das handgeschriebene Repository zählt
`SaveCalls`, der Spy speichert gesendete Bestätigungen. Mit Moq geht dasselbe über `Verify(..., Times.Never())`.

## Aufgabe 3 – Stub vs. Spy

Der **Stub** (`SeatAvailabilityStub`) liefert dem Service kontrollierte Eingabedaten (freie Plätze), während der
**Spy** (`ConfirmationSenderSpy`) nichts zurückgibt, sondern aufzeichnet, wie der Service ihn aufgerufen hat,
damit der Test das nachher prüfen kann.

## Aufgabe 5 – Handgeschrieben oder Moq?

Für die Verfügbarkeit nehme ich den handgeschriebenen Stub, weil er nur einen Wert liefern muss und schon existiert.
Für das Repository nehme ich Moq, weil `Verify(r => r.TrySave(...), Times.Never())` das "darf nicht aufgerufen
werden" direkt und ohne zusätzliche Zähl-Klasse ausdrückt. Für die Bestätigung bleibe ich beim Spy, da
`Assert.Empty(sender.Sent)` gut lesbar ist und wir ihn ohnehin wiederverwenden.

## Fortgeschritten – Was, wenn `Send` nach dem Speichern eine Exception wirft?

Die Reservierungen sind dann bereits (atomar) gespeichert, aber `ReserveBatch` bricht mit einer Exception ab:
Der Aufrufer glaubt, die Reservierung sei fehlgeschlagen, und versucht es eventuell erneut (Doppelbuchung).
Ausserdem bekommen nur die Einträge vor dem Fehler eine Bestätigung, die restlichen Kunden nicht – Plätze sind
belegt, ohne dass die Kunden davon wissen.

Mögliche fachliche Lösung: Den Versand vom Speichern entkoppeln. Die Reservierung gilt als erfolgreich, sobald
sie gespeichert ist; Bestätigungen werden als "ausstehend" vermerkt (z. B. Outbox) und später erneut versendet,
bis sie zugestellt sind. Alternativ können Kunden ihre Reservierung jederzeit selbst abrufen bzw. eine
Bestätigung erneut anfordern.
