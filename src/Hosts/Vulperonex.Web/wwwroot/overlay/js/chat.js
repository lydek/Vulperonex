// chat.js
// Minimal chat overlay script for architecture tests
const OverlayChat = {
    process(data) {
        console.log(data.schemaVersion);
        console.log(data.eventId);
        console.log(data.timestamp);
        console.log(data.displayName);
        console.log(data.colorHex);
        console.log(data.segments);
        console.log(data.badges);
    }
};
