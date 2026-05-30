#include <algorithm>
#include <cstddef>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <iterator>
#include <memory>
#include <optional>
#include <sstream>
#include <stdexcept>
#include <string>
#include <utility>
#include <vector>
#include <windows.h>
#include <commdlg.h>
#include <shlobj.h>

// Reads a local file in binary mode and returns its bytes.
class BinaryFileReader {
public:
    std::vector<unsigned char> readAllBytes(const std::filesystem::path& filePath) const {
        std::ifstream inputFile(filePath, std::ios::binary);

        if (!inputFile) {
            throw std::runtime_error("Could not open input file.");
        }

        return std::vector<unsigned char>(
            std::istreambuf_iterator<char>(inputFile),
            std::istreambuf_iterator<char>());
    }
};

// Provides reusable helper functions for byte pattern matching.
class PatternUtils {
public:
    static bool matchesAt(
        const std::vector<unsigned char>& data,
        std::size_t position,
        const std::vector<unsigned char>& pattern) {
        if (pattern.empty()) {
            return false;
        }

        if (position > data.size() || pattern.size() > data.size() - position) {
            return false;
        }

        return std::equal(
            pattern.begin(),
            pattern.end(),
            data.begin() + static_cast<std::ptrdiff_t>(position));
    }

    static std::optional<std::size_t> findFirst(
        const std::vector<unsigned char>& data,
        const std::vector<unsigned char>& pattern,
        std::size_t startPosition) {
        if (pattern.empty() || startPosition >= data.size() || pattern.size() > data.size()) {
            return std::nullopt;
        }

        for (std::size_t position = startPosition; position <= data.size() - pattern.size(); ++position) {
            if (matchesAt(data, position, pattern)) {
                return position;
            }
        }

        return std::nullopt;
    }
};

// Abstract base class for file signatures.
class FileSignature {
public:
    FileSignature(
        std::string typeName,
        std::string extension,
        std::vector<unsigned char> headerPattern)
        : typeName_(std::move(typeName)),
          extension_(std::move(extension)),
          headerPattern_(std::move(headerPattern)) {}

    virtual ~FileSignature() = default;

    std::string getTypeName() const {
        return typeName_;
    }

    std::string getExtension() const {
        return extension_;
    }

    virtual bool matchesStart(
        const std::vector<unsigned char>& data,
        std::size_t position) const {
        return PatternUtils::matchesAt(data, position, headerPattern_);
    }

    virtual std::optional<std::size_t> findEndExclusive(
        const std::vector<unsigned char>& data,
        std::size_t startPosition) const = 0;

protected:
    std::string typeName_;
    std::string extension_;
    std::vector<unsigned char> headerPattern_;
};

// Base class for signatures that use a fixed footer pattern.
class FixedFooterSignature : public FileSignature {
public:
    FixedFooterSignature(
        std::string typeName,
        std::string extension,
        std::vector<unsigned char> headerPattern,
        std::vector<unsigned char> footerPattern)
        : FileSignature(std::move(typeName), std::move(extension), std::move(headerPattern)),
          footerPattern_(std::move(footerPattern)) {}

    std::optional<std::size_t> findEndExclusive(
        const std::vector<unsigned char>& data,
        std::size_t startPosition) const override {
        const std::size_t searchStart = startPosition + headerPattern_.size();
        const std::optional<std::size_t> footerPosition =
            PatternUtils::findFirst(data, footerPattern_, searchStart);

        if (!footerPosition.has_value()) {
            return std::nullopt;
        }

        return footerPosition.value() + footerPattern_.size();
    }

protected:
    std::vector<unsigned char> footerPattern_;
};

// JPEG file signature definition.
class JpegSignature : public FixedFooterSignature {
public:
    JpegSignature()
        : FixedFooterSignature(
              "JPEG",
              ".jpg",
              {0xFF, 0xD8, 0xFF},
              {0xFF, 0xD9}) {}
};

// PNG file signature definition.
class PngSignature : public FixedFooterSignature {
public:
    PngSignature()
        : FixedFooterSignature(
              "PNG",
              ".png",
              {0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A},
              {0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82}) {}
};

// PDF file signature definition.
class PdfSignature : public FixedFooterSignature {
public:
    PdfSignature()
        : FixedFooterSignature(
              "PDF",
              ".pdf",
              {'%', 'P', 'D', 'F', '-'},
              {'%', '%', 'E', 'O', 'F'}) {}
};

// Stores information and bytes for one carved fragment.
struct CarvedFragment {
    std::string typeName;
    std::string extension;
    std::size_t startOffset;
    std::size_t endExclusive;
    std::vector<unsigned char> content;
};

// Main carving engine that scans data using registered signatures.
class FileCarver {
public:
    void addSignature(std::unique_ptr<FileSignature> signature) {
        signatures_.push_back(std::move(signature));
    }

    std::vector<CarvedFragment> carve(const std::vector<unsigned char>& data) const {
        std::vector<CarvedFragment> fragments;
        std::size_t position = 0;

        while (position < data.size()) {
            bool foundFragment = false;

            for (const std::unique_ptr<FileSignature>& signature : signatures_) {
                if (!signature->matchesStart(data, position)) {
                    continue;
                }

                const std::optional<std::size_t> endExclusive =
                    signature->findEndExclusive(data, position);

                if (!endExclusive.has_value() ||
                    endExclusive.value() <= position ||
                    endExclusive.value() > data.size()) {
                    continue;
                }

                std::vector<unsigned char> content(
                    data.begin() + static_cast<std::ptrdiff_t>(position),
                    data.begin() + static_cast<std::ptrdiff_t>(endExclusive.value()));

                fragments.push_back(CarvedFragment{
                    signature->getTypeName(),
                    signature->getExtension(),
                    position,
                    endExclusive.value(),
                    std::move(content)});

                position = endExclusive.value();
                foundFragment = true;
                break;
            }

            if (!foundFragment) {
                ++position;
            }
        }

        return fragments;
    }

private:
    std::vector<std::unique_ptr<FileSignature>> signatures_;
};

// Writes carved fragments into the selected output directory.
class OutputWriter {
public:
    std::vector<std::filesystem::path> writeFragments(
        const std::vector<CarvedFragment>& fragments,
        const std::filesystem::path& outputDirectory) const {
        std::filesystem::create_directories(outputDirectory);

        std::vector<std::filesystem::path> writtenPaths;

        for (std::size_t index = 0; index < fragments.size(); ++index) {
            const CarvedFragment& fragment = fragments[index];
            const std::filesystem::path outputPath =
                outputDirectory / buildFileName(fragment, index + 1);

            std::ofstream outputFile(outputPath, std::ios::binary);
            if (!outputFile) {
                throw std::runtime_error("Could not create output file.");
            }

            outputFile.write(
                reinterpret_cast<const char*>(fragment.content.data()),
                static_cast<std::streamsize>(fragment.content.size()));

            if (!outputFile) {
                throw std::runtime_error("Could not write output file.");
            }

            writtenPaths.push_back(outputPath);
        }

        return writtenPaths;
    }

private:
    static std::string buildFileName(const CarvedFragment& fragment, std::size_t index) {
        std::ostringstream fileName;
        fileName << "fragment_"
                 << std::setw(3) << std::setfill('0') << index
                 << "_" << fragment.typeName
                 << "_" << fragment.startOffset
                 << fragment.extension;

        return fileName.str();
    }
};

constexpr int InputEditId = 101;
constexpr int BrowseInputButtonId = 102;
constexpr int OutputEditId = 103;
constexpr int BrowseOutputButtonId = 104;
constexpr int StartScanButtonId = 105;
constexpr int ResultEditId = 106;

HWND g_inputEdit = nullptr;
HWND g_outputEdit = nullptr;
HWND g_resultEdit = nullptr;

std::wstring getWindowText(HWND window) {
    const int length = GetWindowTextLengthW(window);
    std::wstring text(static_cast<std::size_t>(length), L'\0');
    GetWindowTextW(window, text.data(), length + 1);
    return text;
}

void setWindowText(HWND window, const std::wstring& text) {
    SetWindowTextW(window, text.c_str());
}

void setResultText(const std::wstring& text) {
    setWindowText(g_resultEdit, text);
}

std::wstring toWideString(const std::string& text) {
    return std::wstring(text.begin(), text.end());
}

std::wstring chooseInputFile(HWND owner) {
    wchar_t filePath[MAX_PATH] = L"";

    OPENFILENAMEW openFileName = {};
    openFileName.lStructSize = sizeof(openFileName);
    openFileName.hwndOwner = owner;
    openFileName.lpstrFile = filePath;
    openFileName.nMaxFile = MAX_PATH;
    openFileName.lpstrFilter = L"All Files\0*.*\0";
    openFileName.nFilterIndex = 1;
    openFileName.Flags = OFN_PATHMUSTEXIST | OFN_FILEMUSTEXIST;

    if (GetOpenFileNameW(&openFileName)) {
        return filePath;
    }

    return L"";
}

std::wstring chooseOutputFolder(HWND owner) {
    BROWSEINFOW browseInfo = {};
    browseInfo.hwndOwner = owner;
    browseInfo.lpszTitle = L"Select output folder";
    browseInfo.ulFlags = BIF_RETURNONLYFSDIRS | BIF_NEWDIALOGSTYLE;

    PIDLIST_ABSOLUTE itemList = SHBrowseForFolderW(&browseInfo);
    if (itemList == nullptr) {
        return L"";
    }

    wchar_t folderPath[MAX_PATH] = L"";
    const BOOL success = SHGetPathFromIDListW(itemList, folderPath);
    CoTaskMemFree(itemList);

    if (success) {
        return folderPath;
    }

    return L"";
}

FileCarver createDefaultCarver() {
    FileCarver carver;
    carver.addSignature(std::make_unique<JpegSignature>());
    carver.addSignature(std::make_unique<PngSignature>());
    carver.addSignature(std::make_unique<PdfSignature>());
    return carver;
}

std::wstring buildResultSummary(
    std::size_t bytesRead,
    const std::vector<CarvedFragment>& fragments,
    const std::vector<std::filesystem::path>& writtenPaths) {
    std::wostringstream result;
    result << L"Bytes read: " << bytesRead << L"\r\n";
    result << L"Fragments found: " << fragments.size() << L"\r\n\r\n";

    for (std::size_t index = 0; index < fragments.size(); ++index) {
        const CarvedFragment& fragment = fragments[index];
        result << index + 1 << L". "
               << toWideString(fragment.typeName)
               << L" start: " << fragment.startOffset
               << L", end: " << fragment.endExclusive
               << L", size: " << fragment.content.size()
               << L" bytes\r\n";
    }

    if (fragments.empty()) {
        result << L"No fragments found. Nothing was written.\r\n";
        return result.str();
    }

    result << L"\r\nWritten output files:\r\n";
    for (const std::filesystem::path& path : writtenPaths) {
        result << L"- " << path.wstring() << L"\r\n";
    }

    return result.str();
}

void startScan(HWND owner) {
    const std::wstring inputPathText = getWindowText(g_inputEdit);
    const std::wstring outputPathText = getWindowText(g_outputEdit);

    if (inputPathText.empty() || outputPathText.empty()) {
        MessageBoxW(owner, L"Please select both an input file and an output folder.", L"Missing input", MB_ICONWARNING);
        return;
    }

    try {
        setResultText(L"Scanning...\r\n");

        const std::filesystem::path inputPath(inputPathText);
        const std::filesystem::path outputPath(outputPathText);

        BinaryFileReader reader;
        const std::vector<unsigned char> fileContent = reader.readAllBytes(inputPath);

        FileCarver carver = createDefaultCarver();
        const std::vector<CarvedFragment> fragments = carver.carve(fileContent);

        std::vector<std::filesystem::path> writtenPaths;
        if (!fragments.empty()) {
            OutputWriter writer;
            writtenPaths = writer.writeFragments(fragments, outputPath);
        }

        setResultText(buildResultSummary(fileContent.size(), fragments, writtenPaths));
    } catch (const std::exception& error) {
        const std::wstring message = L"Error: " + toWideString(error.what());
        setResultText(message);
        MessageBoxW(owner, message.c_str(), L"Error", MB_ICONERROR);
    }
}

HWND createLabel(HWND parent, const wchar_t* text, int x, int y, int width, int height) {
    return CreateWindowExW(
        0,
        L"STATIC",
        text,
        WS_CHILD | WS_VISIBLE,
        x,
        y,
        width,
        height,
        parent,
        nullptr,
        nullptr,
        nullptr);
}

HWND createEdit(HWND parent, int id, int x, int y, int width, int height, DWORD extraStyle = 0) {
    return CreateWindowExW(
        WS_EX_CLIENTEDGE,
        L"EDIT",
        L"",
        WS_CHILD | WS_VISIBLE | WS_TABSTOP | ES_AUTOHSCROLL | extraStyle,
        x,
        y,
        width,
        height,
        parent,
        reinterpret_cast<HMENU>(static_cast<INT_PTR>(id)),
        nullptr,
        nullptr);
}

HWND createButton(HWND parent, int id, const wchar_t* text, int x, int y, int width, int height) {
    return CreateWindowExW(
        0,
        L"BUTTON",
        text,
        WS_CHILD | WS_VISIBLE | WS_TABSTOP,
        x,
        y,
        width,
        height,
        parent,
        reinterpret_cast<HMENU>(static_cast<INT_PTR>(id)),
        nullptr,
        nullptr);
}

void createControls(HWND window) {
    createLabel(window, L"Input file:", 20, 20, 100, 24);
    g_inputEdit = createEdit(window, InputEditId, 120, 18, 430, 26);
    createButton(window, BrowseInputButtonId, L"Browse...", 565, 18, 90, 26);

    createLabel(window, L"Output folder:", 20, 60, 100, 24);
    g_outputEdit = createEdit(window, OutputEditId, 120, 58, 430, 26);
    createButton(window, BrowseOutputButtonId, L"Browse...", 565, 58, 90, 26);

    createButton(window, StartScanButtonId, L"Start Scan", 120, 100, 120, 32);

    createLabel(window, L"Results:", 20, 150, 100, 24);
    g_resultEdit = createEdit(
        window,
        ResultEditId,
        20,
        175,
        635,
        260,
        ES_MULTILINE | ES_AUTOVSCROLL | ES_READONLY | WS_VSCROLL);

    setResultText(L"Select a local input file and output folder, then click Start Scan.");
}

LRESULT CALLBACK windowProcedure(HWND window, UINT message, WPARAM wParam, LPARAM lParam) {
    switch (message) {
    case WM_CREATE:
        createControls(window);
        return 0;

    case WM_COMMAND:
        switch (LOWORD(wParam)) {
        case BrowseInputButtonId: {
            const std::wstring filePath = chooseInputFile(window);
            if (!filePath.empty()) {
                setWindowText(g_inputEdit, filePath);
            }
            return 0;
        }

        case BrowseOutputButtonId: {
            const std::wstring folderPath = chooseOutputFolder(window);
            if (!folderPath.empty()) {
                setWindowText(g_outputEdit, folderPath);
            }
            return 0;
        }

        case StartScanButtonId:
            startScan(window);
            return 0;

        default:
            break;
        }
        break;

    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;

    default:
        break;
    }

    return DefWindowProcW(window, message, wParam, lParam);
}

int WINAPI WinMain(HINSTANCE instance, HINSTANCE, LPSTR, int showCommand) {
    CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);

    const wchar_t className[] = L"FileCarvingToolWindowClass";

    WNDCLASSW windowClass = {};
    windowClass.lpfnWndProc = windowProcedure;
    windowClass.hInstance = instance;
    windowClass.lpszClassName = className;
    windowClass.hCursor = LoadCursorW(nullptr, MAKEINTRESOURCEW(32512));
    windowClass.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1);

    if (!RegisterClassW(&windowClass)) {
        MessageBoxW(nullptr, L"Could not register window class.", L"Error", MB_ICONERROR);
        CoUninitialize();
        return 1;
    }

    HWND window = CreateWindowExW(
        0,
        className,
        L"File Carving Tool",
        WS_OVERLAPPEDWINDOW,
        CW_USEDEFAULT,
        CW_USEDEFAULT,
        700,
        500,
        nullptr,
        nullptr,
        instance,
        nullptr);

    if (window == nullptr) {
        MessageBoxW(nullptr, L"Could not create main window.", L"Error", MB_ICONERROR);
        CoUninitialize();
        return 1;
    }

    ShowWindow(window, showCommand);
    UpdateWindow(window);

    MSG message = {};
    while (GetMessageW(&message, nullptr, 0, 0) > 0) {
        TranslateMessage(&message);
        DispatchMessageW(&message);
    }

    CoUninitialize();
    return static_cast<int>(message.wParam);
}
