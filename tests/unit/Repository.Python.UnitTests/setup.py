"""
Setup configuration for Freezer Lego Meals Python Repository Tests
"""

from setuptools import setup, find_packages

setup(
    name="freezer-lego-meals-repository-python-unit-tests",
    version="1.0.0",
    description="Unit tests for Freezer Lego Meals Python repository layer",
    author="Freezer Lego Meals Team",
    author_email="team@com",
    packages=find_packages(),
    install_requires=[
        "unittest2",  # For unittest support
        "pytest",     # Optional testing enhancement
    ],
    python_requires=">=3.6",
    classifiers=[
        "Development Status :: 4 - Beta",
        "Intended Audience :: Developers",
        "License :: OSI Approved :: MIT License",
        "Programming Language :: Python :: 3",
        "Programming Language :: Python :: 3.6",
        "Programming Language :: Python :: 3.7",
        "Programming Language :: Python :: 3.8",
        "Programming Language :: Python :: 3.9",
    ],
    keywords="freezer lego meals repository python testing",
)