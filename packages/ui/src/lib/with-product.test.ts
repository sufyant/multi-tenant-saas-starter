import { describe, expect, it } from "vitest";
import { withProduct } from "./with-product";

describe("withProduct", () => {
  it("WithProduct_AProductsWordsInATemplatesArea_AddsThemBesideTheTemplates", () => {
    const merged = withProduct(
      { plans: { title: "Plans", meters: { "ai.requests": "AI requests" } } },
      { plans: { meters: { "projects.created": "Projects created" } } },
    );

    expect(merged).toEqual({
      plans: {
        title: "Plans",
        meters: {
          "ai.requests": "AI requests",
          "projects.created": "Projects created",
        },
      },
    });
  });

  it("WithProduct_AProductsArea_AddsIt", () => {
    expect(
      withProduct(
        { plans: { title: "Plans" } },
        { projects: { title: "Projects" } },
      ),
    ).toEqual({
      plans: { title: "Plans" },
      projects: { title: "Projects" },
    });
  });

  it("WithProduct_NoProductWords_IsTheTemplatesWords", () => {
    const template = { plans: { title: "Plans" } };

    expect(withProduct(template, {})).toEqual(template);
  });
});
