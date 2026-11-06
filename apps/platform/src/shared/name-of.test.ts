import { UserResponseStatus } from "@repo/api-client";
import { describe, expect, it } from "vitest";
import { userNameOf } from "./name-of";

const deletedUser = "Deleted user";

describe("userNameOf", () => {
  it("UserNameOf_Deleted_SaysDeletedUserWhateverNameItHolds", () => {
    expect(
      userNameOf(
        {
          name: "Ayşe Yılmaz",
          email: "ayse@northwind.example",
          status: UserResponseStatus.Deleted,
        },
        deletedUser,
      ),
    ).toBe("Deleted user");
  });

  it("UserNameOf_WithAName_ShowsTheWholeName", () => {
    expect(
      userNameOf(
        {
          name: "Ayşe Yılmaz",
          email: "ayse@northwind.example",
          status: UserResponseStatus.Active,
        },
        deletedUser,
      ),
    ).toBe("Ayşe Yılmaz");
  });

  it("UserNameOf_ActiveWithoutAName_ShowsTheEmail", () => {
    expect(
      userNameOf(
        {
          name: null,
          email: "root@starter.example",
          status: UserResponseStatus.Active,
        },
        deletedUser,
      ),
    ).toBe("root@starter.example");
  });
});
