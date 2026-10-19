import { render, screen } from "@testing-library/react";
import { DialogTitle, ThemeProvider, createTheme } from "@mui/material";
import AppDialog from "../AppDialog";

// Pretend the screen is a phone (narrower than the sm breakpoint) or not
const setPhoneScreen = (isPhone) => {
  window.matchMedia = jest.fn().mockImplementation((query) => ({
    matches: isPhone && query.includes("max-width"),
    media: query,
    addListener: jest.fn(),
    removeListener: jest.fn(),
    addEventListener: jest.fn(),
    removeEventListener: jest.fn(),
  }));
};

const paper = () => screen.getByRole("dialog");

describe("AppDialog", () => {
  it("fills the screen edge to edge on phones, with square corners", () => {
    setPhoneScreen(true);
    // Rounds dialog corners the way the app's theme does
    const appTheme = createTheme({ components: { MuiDialog: { styleOverrides: { paper: { borderRadius: 8 } } } } });
    render(
      <ThemeProvider theme={appTheme}>
        <AppDialog open onClose={jest.fn()}>
          <DialogTitle>Edit table</DialogTitle>
        </AppDialog>
      </ThemeProvider>
    );

    expect(paper()).toHaveClass("MuiDialog-paperFullScreen");
    expect(getComputedStyle(paper()).borderRadius).toBe("0");
  });

  it("is a centred, full-width sm dialog on larger screens", () => {
    setPhoneScreen(false);
    render(
      <AppDialog open onClose={jest.fn()}>
        <DialogTitle>Edit table</DialogTitle>
      </AppDialog>
    );

    expect(paper()).not.toHaveClass("MuiDialog-paperFullScreen");
    expect(paper()).toHaveClass("MuiDialog-paperWidthSm", "MuiDialog-paperFullWidth");
  });

  it("lets a dialog choose another width", () => {
    setPhoneScreen(false);
    render(
      <AppDialog open onClose={jest.fn()} maxWidth="md">
        <DialogTitle>Bill summary</DialogTitle>
      </AppDialog>
    );

    expect(paper()).toHaveClass("MuiDialog-paperWidthMd");
  });
});
