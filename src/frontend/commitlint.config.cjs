module.exports = {
  extends: ["@commitlint/config-conventional"],
  rules: {
    "scope-enum": [
      2,
      "always",
      ["phase6", "frontend", "web", "desktop", "docs", "tests"]
    ]
  }
};
