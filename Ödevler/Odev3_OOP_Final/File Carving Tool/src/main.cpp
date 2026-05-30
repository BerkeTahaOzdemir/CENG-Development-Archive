#include <algorithm>
#include <cstddef>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <iostream>
#include <iterator>
#include <memory>
#include <optional>
#include <sstream>
#include <stdexcept>
#include <string>
#include <utility>
#include <vector>

// Reads a local file in binary mode and returns its bytes.
class BinaryFileReader {
public:
    std::vector<unsigned char> readAllBytes(const std::string& filePath) const {
        std::ifstream inputFile(filePath, std::ios::binary);

        if (!inputFile) {
            throw std::runtime_error("Could not open input file: " + filePath);
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
                throw std::runtime_error("Could not create output file: " + outputPath.string());
            }

            outputFile.write(
                reinterpret_cast<const char*>(fragment.content.data()),
                static_cast<std::streamsize>(fragment.content.size()));

            if (!outputFile) {
                throw std::runtime_error("Could not write output file: " + outputPath.string());
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

// Handles command-line arguments and coordinates the program workflow.
class CommandLineApplication {
public:
    int run(int argc, char* argv[]) const {
        if (argc == 1) {
            return runMenu();
        }

        if (argc != 3) {
            printUsage(argv[0]);
            return 1;
        }

        const std::string inputFilePath = argv[1];
        const std::filesystem::path outputDirectoryPath = argv[2];

        return runCarvingWorkflow(inputFilePath, outputDirectoryPath);
    }

private:
    int runMenu() const {
        while (true) {
            std::cout << "\nFile Carving Tool\n";
            std::cout << "1. Scan a file\n";
            std::cout << "2. Show supported file types\n";
            std::cout << "3. About this project\n";
            std::cout << "4. Exit\n";
            std::cout << "Choose an option: ";

            std::string choice;
            std::getline(std::cin, choice);

            if (choice == "1") {
                scanFileFromMenu();
            } else if (choice == "2") {
                showSupportedFileTypes();
            } else if (choice == "3") {
                showAboutProject();
            } else if (choice == "4") {
                std::cout << "Goodbye.\n";
                return 0;
            } else {
                std::cout << "Invalid option. Please choose 1, 2, 3, or 4.\n";
            }
        }
    }

    void scanFileFromMenu() const {
        std::string inputFilePath;
        std::string outputDirectoryPath;

        std::cout << "Enter input file path: ";
        std::getline(std::cin, inputFilePath);

        std::cout << "Enter output directory path: ";
        std::getline(std::cin, outputDirectoryPath);

        if (inputFilePath.empty() || outputDirectoryPath.empty()) {
            std::cout << "Input file path and output directory path cannot be empty.\n";
            return;
        }

        runCarvingWorkflow(inputFilePath, outputDirectoryPath);
    }

    int runCarvingWorkflow(
        const std::string& inputFilePath,
        const std::filesystem::path& outputDirectoryPath) const {
        std::cout << "File Carving Tool\n";
        std::cout << "Input file: " << inputFilePath << '\n';
        std::cout << "Output directory: " << outputDirectoryPath << "\n\n";

        try {
            BinaryFileReader reader;
            const std::vector<unsigned char> fileContent =
                reader.readAllBytes(inputFilePath);

            FileCarver carver;
            carver.addSignature(std::make_unique<JpegSignature>());
            carver.addSignature(std::make_unique<PngSignature>());
            carver.addSignature(std::make_unique<PdfSignature>());

            const std::vector<CarvedFragment> fragments = carver.carve(fileContent);

            printSummary(fileContent.size(), fragments);

            if (fragments.empty()) {
                std::cout << "\nNo fragments found. There is nothing to write.\n";
                return 0;
            }

            OutputWriter writer;
            const std::vector<std::filesystem::path> writtenPaths =
                writer.writeFragments(fragments, outputDirectoryPath);

            std::cout << "\nWritten output files:\n";
            for (const std::filesystem::path& path : writtenPaths) {
                std::cout << "- " << path << '\n';
            }

            return 0;
        } catch (const std::exception& error) {
            std::cerr << "Error: " << error.what() << '\n';
            return 2;
        }
    }

    void printUsage(const std::string& programName) const {
        std::cout << "File Carving Tool - Educational C++ OOP Project\n\n";
        std::cout << "Usage:\n";
        std::cout << "  " << programName << " <input_file_path> <output_directory_path>\n";
        std::cout << "  " << programName << "\n";
    }

    void printSummary(
        std::size_t bytesRead,
        const std::vector<CarvedFragment>& fragments) const {
        std::cout << "Bytes read: " << bytesRead << '\n';
        std::cout << "Fragments found: " << fragments.size() << '\n';

        for (std::size_t index = 0; index < fragments.size(); ++index) {
            const CarvedFragment& fragment = fragments[index];

            std::cout << index + 1 << ". "
                      << fragment.typeName
                      << " start: " << fragment.startOffset
                      << ", end: " << fragment.endExclusive
                      << ", size: " << fragment.content.size()
                      << " bytes\n";
        }
    }

    void showSupportedFileTypes() const {
        std::cout << "\nSupported file types:\n";
        std::cout << "- JPEG (.jpg)\n";
        std::cout << "- PNG (.png)\n";
        std::cout << "- PDF (.pdf)\n";
    }

    void showAboutProject() const {
        std::cout << "\nAbout this project:\n";
        std::cout << "This is a simple educational C++17 OOP file carving tool.\n";
        std::cout << "It scans a local input file for basic JPG, PNG, and PDF signatures.\n";
        std::cout << "It is designed for defensive learning and university assignment use.\n";
        std::cout << "It is not a professional forensic solution.\n";
    }
};

int main(int argc, char* argv[]) {
    CommandLineApplication app;
    return app.run(argc, argv);
}
