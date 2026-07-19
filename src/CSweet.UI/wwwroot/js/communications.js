export function isDocumentVisible() {
    return document.visibilityState === "visible";
}

export function scrollToBottom(element) {
    if (!element) return;
    element.scrollTop = element.scrollHeight;
}
