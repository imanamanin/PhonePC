document.getElementById("open").addEventListener("click", () => {
  chrome.runtime.sendMessage({ type: "open-viewer" });
  window.close();
});
