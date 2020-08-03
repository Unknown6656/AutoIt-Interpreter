#include <iostream>


extern "C" __declspec(dllexport) int32_t __cdecl add(int32_t a, int32_t b)
{
    return a + b;
}

extern "C" __declspec(dllexport) void __cdecl say_hello(void)
{
    std::cout << "Hello World from C++!\n";
}


int main(const int argc, const char** argv)
{
    std::cout << "Hello World!\n";

    return 0;
}
