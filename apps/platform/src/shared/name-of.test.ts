import { UserResponseStatus } from "@repo/api-client";
import { describe, expect, it } from "vitest";
import { userNameOf } from "./name-of";

const deletedUser = "Deleted user";

describe("userNameOf", () => {
  it("UserNameOf_Deleted_SaysDeletedUserWhateverNamesItHolds", () => {
    expect(
      userNameOf(
        {
          firstName: "Ayşe",
          lastName: "Yılmaz",
          email: "ayse@northwind.example",
          status: UserResponseStatus.Deleted,
        },
        deletedUser,
      ),
    ).toBe("Deleted user");
  });

  it("UserNameOf_WithNames_ShowsTheNames", () => {
    expect(
      userNameOf(
        {
          firstName: "Ayşe",
          lastName: "Yılmaz",
          email: "ayse@northwind.example",
          status: UserResponseStatus.Active,
        },
        deletedUser,
      ),
    ).toBe("Ayşe Yılmaz");
  });

  it("UserNameOf_ActiveWithoutNames_ShowsTheEmail", () => {
    expect(
      userNameOf(
        {
          firstName: null,
          lastName: null,
          email: "root@starter.example",
          status: UserResponseStatus.Active,
        },
        deletedUser,
      ),
    ).toBe("root@starter.example");
  });
});
