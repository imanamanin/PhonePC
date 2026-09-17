chrome.sidePanel.setPanelBehavior({ openPanelOnActionClick: true }).catch(() => {
  // Older Chrome without side panel still loads the viewer from the action.
});

chrome.runtime.onInstalled.addListener(() => {
  chrome.sidePanel.setPanelBehavior({ openPanelOnActionClick: true }).catch(() => {});
});

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  if (message?.type === "open-window") {
    chrome.windows.create({
      url: chrome.runtime.getURL("viewer.html") + "?mode=window",
      type: "popup",
      width: 400,
      height: 820,
      focused: true
    });
    sendResponse({ ok: true });
    return true;
  }
  if (message?.type === "open-viewer") {
    chrome.tabs.create({ url: chrome.runtime.getURL("viewer.html") });
    sendResponse({ ok: true });
    return true;
  }
  return false;
});
